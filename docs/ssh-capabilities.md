# SSH capabilities

Transport: SSH.NET 2026.0.0 + SshNet.Agent 2026.0.0. Rationale: [decisions/0001-ssh-transport.md](decisions/0001-ssh-transport.md).

Status legend: **supported** = implemented and covered by integration tests; **partial** = works with stated limits; **unsupported** = not available in the transport or not implemented yet.

| Capability | Status | Notes |
|---|---|---|
| Interactive shell with PTY | supported | `ITerminalSessionFactory.OpenAsync`, terminal type defaults to `xterm-256color` |
| Terminal resize | supported | `ITerminalSession.Resize`, sends SSH `window-change` |
| Multiple independent sessions | supported | one SSH connection per session |
| Streaming terminal I/O | supported | raw bytes, cancellable `ReadAsync` |
| Host key verification hook | supported | `IHostKeyVerifier` gets endpoint, algorithm, `SHA256:` fingerprint (same format as `ssh-keygen -lf`); fails closed |
| Changed host key detection | supported | `known_hosts` (read-only, hashed entries, `@revoked`) plus app-trusted keys in SQLite; unknown keys pause for a user decision, changed keys are blocked. See `docs/decisions/host-key-verification.md` |
| Private key auth (OpenSSH ed25519, RSA rsa-sha2, passphrase) | supported | keys are referenced by file path |
| Password auth | supported | |
| SSH agent (OpenSSH agent, Unix socket) | supported | via SshNet.Agent; `SSH_AUTH_SOCK` or explicit socket path |
| SSH agent (Windows OpenSSH pipe, Pageant) | partial | provided by SshNet.Agent, not verified by tests |
| ProxyJump | partial | a chain through `JumpHost` using a local loopback forward; one level tested |
| `~/.ssh/config` import | partial | app-side parser, see [saved-connections.md](saved-connections.md): `Host` (with `*`, `?`, `!` patterns), `Include`, `HostName`, `User`, `Port`, `IdentityFile` (first one), `ProxyJump` (single hop to another imported alias). `Match`, percent tokens and every other directive are reported and ignored |
| SFTP list / upload / download | supported | errors: `RemotePathNotFound`, `PermissionDenied`, `Cancelled` |
| SFTP stat / mkdir / rename / delete | supported | files and empty directories; symlink kind (target kind or broken), permissions; see [file-browsing.md](file-browsing.md) |
| SFTP progress, resume | unsupported | SSH.NET has progress callbacks; not exposed yet (Transfer Queue task) |
| Keyboard-interactive / 2FA | unsupported | not implemented |
| User and host certificates | unsupported | not implemented |
| FIDO (`sk-*`) keys | partial | only through an agent, not tested |
| Agent forwarding, X11 forwarding | unsupported | |
| Keepalive / reconnect | unsupported | Reconnect Policy task |

## Platforms

| Platform | Status |
|---|---|
| Linux | integration tests against OpenSSH in Docker run in CI (`ubuntu-latest`) |
| macOS | integration tests pass locally with Docker Desktop; in CI only the offline tests run, because GitHub macOS runners have no Docker |
| Windows | in CI only the offline tests run, because Windows runners cannot start Linux containers; agent pipe and Pageant are not verified |
