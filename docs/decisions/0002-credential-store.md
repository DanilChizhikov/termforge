# 0002. Credential store

- Status: accepted
- Date: 2026-10-25
- Scope: `src/Api/Core/Credentials`, `src/Infrastructure/Credentials`, `src/Api/Entrypoint/Credentials`

## Context

The application stores passwords and tokens (SSH passwords, key passphrases, API tokens). `PROJECT_SPEC.md` (sections 3 and 11) requires the operating system credential store where possible and an encrypted fallback vault with a master password only when the OS store is unavailable. Secrets must never reach logs, exception messages, `ToString()` output or operation metadata.

The application ships as a self-contained, unsigned build for Windows, macOS and Linux, so every option had to work without installers, code signing entitlements or a desktop UI toolkit.

## Options considered

### OS store access

| Option | Windows | macOS | Linux | Verdict |
|---|---|---|---|---|
| Own P/Invoke to the native APIs | `advapi32` `CredWriteW` / `CredReadW` / `CredDeleteW` | `Security.framework` `SecItemAdd` / `SecItemCopyMatching` / `SecItemUpdate` / `SecItemDelete` | `libsecret-1.so.0` `secret_password_storev_sync` / `lookupv_sync` / `clearv_sync` | **chosen** |
| `Microsoft.Identity.Client.Extensions.Msal` cache helpers | DPAPI file | Keychain | libsecret | rejected: built to persist one MSAL token cache blob, not a key/value store; brings the MSAL dependency tree |
| Windows-only wrappers (e.g. `Meziantou.Framework.Win32.CredentialManager`) | yes | no | no | rejected: covers one platform; the Windows part is only ~100 lines of P/Invoke |
| Git Credential Manager | yes | yes | yes | rejected as a dependency: not published as a reusable library. It uses the same native APIs, which confirms the approach |
| `secret-tool` CLI on Linux (secret via stdin) | - | - | yes | rejected: needs the extra `libsecret-tools` package, which many desktops do not install by default, and starts a process per operation. `libsecret-1-0` itself is installed wherever a Secret Service desktop exists |

The native APIs are small, stable, and have been part of each OS for a long time. Owning about 150 lines of interop per platform costs less than adding and tracking a dependency, and it lets every error map to an actionable diagnostic.

Notes on each implementation:

- **Windows**: generic credentials named `termforge:<key>`, `CRED_PERSIST_LOCAL_MACHINE`, at most 2560 bytes (`CRED_MAX_CREDENTIAL_BLOB_SIZE`). `ERROR_NO_SUCH_LOGON_SESSION` (service or network logon) is reported as "store unavailable".
- **macOS**: generic password items with service `termforge`, account `<key>`, in the default (login) keychain. The unsigned app cannot use the data protection keychain, which requires an entitlement. Reads pass `kSecUseAuthenticationUIFail` so that a locked keychain fails instead of showing a prompt. The keychain ACL trusts the binary that created an item, so a different binary (for example a rebuilt or updated app) can trigger a one-time "allow access" prompt.
- **Linux**: schema `com.dtech.termforge.Credential` with the single attribute `key`, stored in the default collection. libsecret's API stores text, so values must be valid UTF-8 without NUL. The varargs `secret_password_store_sync` cannot be called safely from .NET, so the `*v_sync` variants are used with a `GHashTable` of attributes.

### Fallback vault key derivation

- **Argon2id**: memory-hard and the stronger choice against GPU attacks, but .NET 10 has no built-in implementation, so it would require a third-party package (managed Argon2 or libsodium bindings with native binaries for six RIDs).
- **PBKDF2-HMAC-SHA256** (`Rfc2898DeriveBytes.Pbkdf2`, built in): **chosen**, with 600,000 iterations (the OWASP 2023 recommendation for PBKDF2-HMAC-SHA256). The iteration count and KDF id are stored in the header, so the cost can be raised, or the KDF switched to Argon2id, in a later format version without breaking existing vaults.

## Decision

### Abstractions (Core)

- `ICredentialStore`: `Store` / `Read` / `Delete` a `Secret` by `CredentialKey`. Every operation returns `CredentialResult` / `CredentialResult<T>` with a `CredentialError(Code, Message)`. Expected failures are never thrown as exceptions.
- `Secret`: owns a byte buffer, `ToString()` and the debugger display return `[redacted]`, `Dispose()` zeroes the buffer, and JSON serialization exposes only `Length`.
- `CredentialKey`: 1 to 200 characters from ASCII letters, digits and `._-:/@`.
- `ICredentialVault` (unlock / lock / state) and `ICredentialStoreStatusProvider` (selected backend plus diagnostics).

### Backend selection

`CredentialBackendSelector` runs once, lazily, on first use:

1. `IOperatingSystemCredentialStore.CheckPrerequisites()` checks static requirements: the native library loads, and on Linux a D-Bus session bus exists.
2. A self-test stores, reads back, compares and deletes a random probe value under `termforge-availability-probe`, with a timeout (`Credentials:ProbeTimeout`, default 10 s) so a locked store waiting for a dialog cannot hang startup.
3. If the vault file already exists, the vault stays selected even when the OS store works, so secrets saved earlier remain reachable. Otherwise the OS store is used when the self-test passed, and the vault when it failed.

The reason for the selection and the OS store diagnostic are logged (warning on fallback) and returned by `GET /api/credentials/status`. A failure after selection is returned as an error and never switches backends silently, because that would split secrets across two stores. The backend is chosen once per process, so a store that becomes available later is picked up after a restart.

### Vault file format v1

All integers are big-endian.

| Offset | Size | Field |
|---|---|---|
| 0 | 4 | magic `TFVT` |
| 4 | 2 | format version (`1`) |
| 6 | 1 | KDF id (`1` = PBKDF2-HMAC-SHA256) |
| 7 | 1 | reserved (`0`) |
| 8 | 4 | KDF iterations |
| 12 | 32 | salt (random per vault) |
| 44 | 32 | password verifier |
| 76 | 12 | AES-GCM nonce (random per write) |
| 88 | 4 | ciphertext length |
| 92 | n | AES-256-GCM ciphertext |
| 92+n | 16 | GCM tag |

- PBKDF2 produces a 32-byte master key. HKDF-Expand with two different `info` labels derives the AES-256 key and the verifier, and the master key is then zeroed.
- The whole 92-byte header is the GCM associated data, so any header change (version, KDF settings, nonce, length) fails authentication.
- The verifier separates "wrong master password" from "file tampered or damaged". It gives an attacker nothing beyond what the GCM tag already gives: every guess still costs a full PBKDF2 run.
- The plaintext is a binary list of `(key, value)` entries, never JSON, so no immutable strings hold secret values. Plaintext buffers are zeroed after use.
- Writes go to a temporary file in the same directory (`FileMode.CreateNew`, write-through, `fsync`), followed by an atomic rename over the vault. On Unix the file is created `0600` and the directory `0700`.
- The vault holds only the derived keys in memory while unlocked. Each read decrypts the file and keeps only the requested value. `Lock()` zeroes the keys.
- The first unlock creates the vault and requires a master password of at least 8 characters.

### HTTP API (no UI)

`MapCredentialEndpoints()` maps:

- `GET /api/credentials/status`: backend, selection reason, OS store availability and diagnostic, vault state.
- `POST /api/credentials/vault/unlock` with `{ "masterPassword": "..." }`: `204`, or a problem response with `code` (`InvalidMasterPassword`, `WeakMasterPassword` → 400; `VaultNotInUse`, `VaultCorrupted`, `UnsupportedVaultVersion` → 409).
- `POST /api/credentials/vault/lock`: `204`.

The endpoints live under `/api`, so the local host security middleware ([../security.md](../security.md)) protects them:

- Host header allow-list.
- The `termforge_session` cookie (`401` without it).
- Exact `Origin` and `X-CSRF-Token` on `POST` (`403` without them).

Tests in `Entrypoint.Tests/Credentials` run against the full pipeline and cover the missing-session and missing-CSRF cases for unlock, lock and status. Within an authenticated session, the only brute-force throttle is the KDF cost (about 90 ms per attempt, measured on an Apple Silicon laptop). Rate limiting is a follow-up.

## Consequences

- No third-party dependencies. `Credentials.csproj` references the ASP.NET Core shared framework (DI, options, logging) and enables unsafe code for interop.
- Zeroing is best-effort. The master password arrives as a JSON `string` that cannot be wiped, CoreFoundation and libsecret keep their own copies, and the GC may move managed arrays before they are zeroed.
- Linux needs a running Secret Service provider with an unlocked default collection. Headless servers and most CI runners fall back to the vault.
- macOS may show a keychain access prompt when a different binary reads an item created by an earlier build.

## Verification

| Platform | How | Result |
|---|---|---|
| macOS 26 (Darwin 25.6, arm64), local | `dotnet test`, real login keychain | OS store self-test passed, round-trip / replace / delete / not-found passed, no leftover items |
| Linux arm64 (Docker `mcr.microsoft.com/dotnet/sdk:10.0`, Debian, gnome-keyring), local | no D-Bus session | round-trip skipped with the "No D-Bus session bus" diagnostic |
| same | D-Bus + gnome-keyring without an unlocked collection | skipped with "Object does not exist at path /org/freedesktop/secrets/collection/login" diagnostic, no hang |
| same | D-Bus + unlocked gnome-keyring | all tests passed, repeated 5 times, no leftover items |
| CI `ubuntu-latest`, `macos-latest`, `windows-latest` | see PR checks | recorded in the PR description |

Linux x64 and Windows were not tested locally.
