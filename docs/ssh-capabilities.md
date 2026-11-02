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
| ProxyJump | partial | a chain through `JumpHost` using a local loopback forward that remains active for the session; one level tested. Listener isolation is tracked in [#76](https://github.com/DanilChizhikov/termforge/issues/76) |
| `~/.ssh/config` import | partial | app-side parser, see [saved-connections.md](saved-connections.md): `Host` (with `*`, `?`, `!` patterns), `Include`, `HostName`, `User`, `Port`, `IdentityFile` (first one), `ProxyJump` (single hop to another imported alias). `Match`, percent tokens and every other directive are reported and ignored |
| SFTP list / upload / download | supported | errors: `RemotePathNotFound`, `PermissionDenied`, `Cancelled` |
| SFTP stat / mkdir / rename / delete | supported | files and empty directories; symlink kind (target kind or broken), permissions; see [file-browsing.md](file-browsing.md) |
| SFTP progress, cancel, resume | supported | transfer queue with live progress and cancel; retry appends to the kept `.part` file. See [decisions/transfer-queue.md](decisions/transfer-queue.md) |
| Keyboard-interactive / 2FA | unsupported | not implemented |
| User and host certificates | unsupported | not implemented |
| FIDO (`sk-*`) keys | partial | only through an agent, not tested |
| Agent forwarding, X11 forwarding | unsupported | |
| Reconnect | supported | per-server policy (manual, automatic with backoff, off); a reconnect opens a new shell. See [reconnect-and-command-history.md](reconnect-and-command-history.md) |
| Keepalive | supported | SSH keepalive packets every 30 seconds, plus an active connection check with a 10-second deadline; connection errors and failed checks terminate the affected session |

## Connection health

Terminal and command connections check `exec true` every 30 seconds; their SSH accounts must allow that command. SFTP connections check directory attributes and do not require shell access. Jump connections send keepalives and report transport errors without executing commands; the final connection's active check also detects a stalled jump path.

If a check does not respond within 10 seconds, TermForge closes the SSH client, releases the blocked probe and reports the connection failure to the terminal. A terminal with automatic reconnect then follows its existing reconnect policy. This detects silently dropped traffic as well as explicit connection errors; keepalive packets alone do not provide a response deadline.

## Platforms

| Platform | Status |
|---|---|
| Linux | integration tests against OpenSSH in Docker run in CI (`ubuntu-latest`) |
| macOS | integration tests pass locally with Docker Desktop; CI does not run on macOS |
| Windows | not tested in CI (CI runs on Linux only); the release workflow smoke-tests the `win-x64` build; agent pipe and Pageant are not verified |
