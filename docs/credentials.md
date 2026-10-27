# Credentials: usage, security and troubleshooting

Design and trade-offs: [decisions/0002-credential-store.md](decisions/0002-credential-store.md).

## How secrets are stored

1. On first use the backend checks the operating system credential store by storing, reading and deleting a probe value.
2. If the check passes and no vault file exists yet, secrets go to the OS store:
   - Windows: Credential Manager, generic credentials named `termforge:<key>`
   - macOS: login keychain, generic passwords with service `termforge`
   - Linux: Secret Service (GNOME Keyring, KWallet, KeePassXC) through libsecret, schema `com.dtech.termforge.Credential`
3. If the check fails, secrets go to the encrypted vault file. The vault must be unlocked with a master password after every application start. The first unlock creates the vault (master password of at least 8 characters).
4. Once a vault file exists it stays in use, even if the OS store becomes available later, so no secret becomes unreachable.

| Endpoint | Result |
|---|---|
| `GET /api/credentials/status` | `200` with the active backend, the reason it was selected, the OS store availability and diagnostic, and the vault state (`NotCreated`, `Locked`, `Unlocked`) |
| `POST /api/credentials/vault/unlock` with `{"masterPassword":"..."}` | `204`, or a problem response with `code` (`InvalidMasterPassword` and `WeakMasterPassword` → `400`; `VaultNotInUse`, `VaultCorrupted` and `UnsupportedVaultVersion` → `409`) |
| `POST /api/credentials/vault/lock` | `204` |

Like every `/api` endpoint, these are protected by the browser session described in [security.md](security.md):

- Each request needs the `termforge_session` cookie (`401` without it).
- `POST` requests also need a matching `Origin` and the `X-CSRF-Token` header (`403` without it).

So a plain `curl` call from another process is rejected. Until there is a UI, unlock the vault from the browser tab that TermForge opened, using the developer console:

```js
const { csrfToken } = await (await fetch('/api/session')).json();
await fetch('/api/credentials/vault/unlock', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', 'X-CSRF-Token': csrfToken },
  body: JSON.stringify({ masterPassword: '...' }),
});
```

## Configuration

`appsettings.json`, section `Credentials`:

| Setting | Default | Meaning |
|---|---|---|
| `VaultPath` | `<LocalApplicationData>/termforge/credentials.vault` | vault file location (`%LOCALAPPDATA%` on Windows, `~/Library/Application Support` on macOS, `~/.local/share` on Linux) |
| `VaultKdfIterations` | `600000` | PBKDF2-HMAC-SHA256 iterations for newly created vaults; existing vaults keep the count stored in their header |
| `ProbeTimeout` | `00:00:10` | how long the OS store self-test may take before the store counts as unavailable |

## Security properties

- Secrets are wrapped in `Secret`: `ToString()` returns `[redacted]` and `Dispose()` zeroes the buffer. Logs contain backend names, error codes and diagnostics, never secret values or the master password.
- Vault: PBKDF2-HMAC-SHA256 (600,000 iterations), AES-256-GCM with a fresh random nonce on every write, the whole header authenticated as associated data, versioned format, atomic replace, `0600` file permissions on Unix. Nothing is written to disk in plaintext.
- A wrong master password (`InvalidMasterPassword`) is reported separately from a modified or damaged file (`VaultCorrupted`) and from a file written by a newer version (`UnsupportedVaultVersion`).
- Secrets leave the credential store only through the settings export, and only into a separate archive encrypted with the vault crypto after explicit confirmation and an archive password (`docs/decisions/settings-import-and-export.md`). They are never written in plaintext.

## Troubleshooting

| Diagnostic | Fix |
|---|---|
| `libsecret-1.so.0 is not installed` | `sudo apt install libsecret-1-0` (Debian/Ubuntu) or `sudo dnf install libsecret`, plus a Secret Service provider such as `gnome-keyring` or KeePassXC with Secret Service integration enabled |
| `No D-Bus session bus was found` | start the app from a desktop session; on a headless machine use `dbus-run-session` with an unlocked keyring, or use the vault |
| `Object does not exist at path /org/freedesktop/secrets/collection/login` / collection locked | create or unlock the default keyring (for example log in to the desktop session, or `gnome-keyring-daemon --unlock`) |
| `The macOS Keychain refused access` | unlock the login keychain (`security unlock-keychain ~/Library/Keychains/login.keychain-db`) and allow access when asked; a rebuilt or updated binary may ask once |
| `did not respond within 10 seconds` | the store is waiting for a dialog or is locked; unlock it and restart the application |
| `not available for this logon session` (Windows) | run the application from an interactive user session, not as a service |
| `The encrypted credential vault is damaged or was modified` | restore the vault file from a backup; the application never overwrites a vault it cannot authenticate |

The backend is chosen once per process: restart the application after fixing the OS store. If a vault file already exists, it stays in use until it is removed. Removing it deletes the secrets stored in it.

## Known limitations

- Linux Secret Service stores text secrets only (valid UTF-8, no NUL). Windows Credential Manager limits a secret to 2560 bytes.
- Zeroing sensitive memory is best-effort: the master password arrives as an immutable JSON string, native stores keep their own copies, and the garbage collector may move managed buffers before they are zeroed.
- No rate limiting on vault unlock attempts yet; each attempt costs one full PBKDF2 derivation.
- There is no migration between the vault and the OS store yet.
