# 0001. SSH transport for terminals and SFTP

- Status: accepted
- Date: 2026-10-25
- Scope: `src/Api/Core` (transport abstractions), `src/Infrastructure/Ssh` (implementation), `src/Tests/Ssh.IntegrationTests` (evidence)

## Context

The backend needs interactive terminal sessions with a real PTY and resize, plus SFTP, on Windows, macOS and Linux, from a self-contained .NET build. The spec asks to validate PTY, resize, agent, `~/.ssh/config`, ProxyJump, host key verification, SFTP and cross-platform behaviour before choosing libraries, and lists "system OpenSSH for terminals + .NET library for SFTP" as a candidate.

## Decision

Use **SSH.NET (`SSH.NET` 2026.0.0, latest stable)** for both terminal sessions and SFTP, plus **`SshNet.Agent` 2026.0.0** for SSH agent authentication. One connector builds every connection, so terminal and SFTP share the same authentication, host key verification and jump host logic.

Core exposes only what callers need:

- `Connections`: `SshConnectionTarget` (endpoint, user, authentication, optional `JumpHost`), `SshAuthentication` (`PrivateKeyAuthentication`, `AgentAuthentication`, `PasswordAuthentication`), `IHostKeyVerifier` (gets endpoint, algorithm and `SHA256:` fingerprint, returns `Trust`/`Reject`), `SshResult`/`SshError`/`SshErrorKind`.
- `Terminal`: `ITerminalSessionFactory`, `ITerminalSession` (read, write, resize, dispose), `TerminalSize`, `TerminalSessionRequest`.
- `Files`: `IRemoteFileClientFactory`, `IRemoteFileClient` (list, upload, download), `RemoteFileEntry`.

`services.AddSshTransport()` registers the implementation. The host must register an `IHostKeyVerifier`, and without one, resolving the factories fails. Every failure comes back as an `SshResult` with an `SshErrorKind`. Library exceptions do not leave the infrastructure layer.

## Verified capabilities

All of the following are covered by executable tests in `src/Tests/Ssh.IntegrationTests`. They run against OpenSSH on Alpine 3.22 in Docker through Testcontainers: a target container plus a jump container on a private Docker network, with host keys and user keys generated at container start. All 35 tests passed locally on macOS (arm64, Docker Desktop) and run on the ubuntu CI runner.

| Capability | Evidence |
|---|---|
| Interactive shell with a PTY | `tty` reports `/dev/pts/N`, `$TERM` equals the requested type (`TerminalSessionTests`) |
| Streaming I/O | commands are written and their output read back through `ITerminalSession` |
| Resize | `stty size` reports `24 80` initially and `40 132` after `Resize(132, 40)` (SSH `window-change` via `ShellStream.ChangeWindowSize`) |
| Independent sessions | two sessions on the same server keep different window sizes |
| Session close | remote `exit` ends the read stream, `IsOpen` turns false, further writes return `SessionClosed` |
| Host key verification hook | the verifier gets the endpoint, `ssh-ed25519` and a `SHA256:` fingerprint equal to the one computed independently from the server's public key file. A rejected key fails with `HostKeyRejected` and `PresentedHostKey` for terminal and SFTP. A changed key (pinned key mismatch) is rejected. A throwing verifier means rejection |
| Private key auth | OpenSSH-format ed25519, RSA 3072 (rsa-sha2), passphrase-protected ed25519. A wrong key returns `AuthenticationFailed`. A missing passphrase, missing file or malformed file returns `PrivateKeyUnusable` |
| Password auth | works. A wrong password returns `AuthenticationFailed` without the password in the message |
| SSH agent | a real `ssh-agent` started by the test holds the key and authentication succeeds through `AgentAuthentication(socketPath)`. An empty agent or unreachable socket returns `AgentUnavailable` |
| ProxyJump | terminal and SFTP reach `ssh-target:22`, a name that only resolves inside the Docker network, through the jump container. Both host keys are verified, each against its own endpoint. A rejected jump host key stops the chain before the target is contacted |
| SFTP | 256 KiB upload, listing (kind, size, full path, `.`/`..` filtered), byte-exact download. A missing path returns `RemotePathNotFound`. A write to `/etc` returns `PermissionDenied`. Cancellation returns `Cancelled` |
| Offline error paths | closed port returns `ConnectionFailed`, invalid terminal size fails before connecting, `ToString()` of credentials hides secrets. These tests run on all three CI OS |

## Unsupported or partial

- **`~/.ssh/config`**: SSH.NET has no ssh_config parser. Its public API has no config types, so `Host` aliases, `HostName`, `User`, `Port`, `IdentityFile`, `ProxyJump`, `Include` and `Match` are not read. Alias resolution must happen in the application ("Saved Connections and SSH Config Import" task). That task maps a supported subset onto `SshConnectionTarget` and reports unsupported directives.
- **ProxyJump**: SSH.NET has no ProxyJump. The `direct-tcpip` channel type is internal and its only public API is `ForwardedPortLocal`, so the connector connects to the jump host, opens a `ForwardedPortLocal` on `127.0.0.1` with an ephemeral port, and dials the target through it. Chains of jump hosts work recursively through `JumpHost.JumpHost`. Only one level is tested.
- **SSH agent**: SSH.NET has no agent support. `SshNet.Agent` is a third-party package (MIT, single maintainer, version pinned to the SSH.NET major). It speaks the OpenSSH agent protocol over a Unix socket (tested) and over the Windows OpenSSH agent named pipe or Pageant (not tested). `AgentAuthentication(null)` uses `SSH_AUTH_SOCK`, or the default Windows pipe. Agent forwarding to the remote host is not supported.
- **Interactive host key prompts**: SSH.NET raises `HostKeyReceived` synchronously during key exchange, so `IHostKeyVerifier` is synchronous. A UI "trust this new key?" flow works as: reject, show `SshError.PresentedHostKey` to the user, store the decision, reconnect. The known-hosts store itself belongs to the "Host Key Verification" task.
- **Keyboard-interactive / 2FA, user certificates, host certificates, FIDO (`sk-*`) keys in files**: not implemented in the prototype and not tested. SSH.NET has keyboard-interactive and certificate types. FIDO keys are only possible through the agent.
- **Keepalive and reconnect**: not configured yet (`KeepAliveInterval` exists in SSH.NET). Deferred to the reconnect task.

## Security implications

- SSH.NET trusts **any** host key when no `HostKeyReceived` handler is attached. The connector always attaches one and fails closed: verifier exceptions mean rejection. Rejection happens during key exchange, before any credential is sent.
- The fingerprint format (`SHA256:` + unpadded base64) matches `ssh-keygen -lf`, so stored fingerprints and OpenSSH `known_hosts` data can be compared directly.
- The ProxyJump tunnel listens on `127.0.0.1` on a random port while the connection is open. Any local process can connect to that port and reach the target's SSH port through the jump host. It still has to pass the target's own authentication, and the app still verifies the target host key. This matches the single-local-user threat model, but the tunnel must never bind to a non-loopback address.
- Private keys are referenced by path and loaded into process memory by SSH.NET for the connection's lifetime. With an agent, keys never enter the process.
- `PasswordAuthentication` and `PrivateKeyAuthentication` override `ToString()` so passwords and passphrases do not reach logs through record formatting. Error messages contain endpoints and fingerprints, never secrets.
- Terminal output is passed through as raw bytes and is not logged or stored by the transport.

## Cross-platform limits

- SSH.NET and SshNet.Agent are fully managed (net8.0/net10.0 assemblies, BouncyCastle as a managed dependency). There is no native PTY or `ssh` binary to ship, so self-contained builds need nothing extra per OS.
- Docker-based tests run only on `ubuntu-latest`. GitHub's macOS runners have no Docker, and Windows runners only run Windows containers. On those runners `TERMFORGE_SKIP_DOCKER_TESTS=true` (set in `ci.yml`) marks the tests inconclusive (skipped) with a reason. They are also skipped when Testcontainers finds no reachable Docker endpoint (`DockerUnavailableException`). The offline tests still run on all three OS.
- Not verified: Windows agent named pipe and Pageant, SSH.NET client behaviour on a Windows host against a real server, macOS and Windows behaviour in CI. Local macOS runs used Docker Desktop.
- Each terminal session uses one dedicated thread, because `ShellStream.Read` blocks and SSH.NET 2026.0.0 has no async read override. The thread pumps output into a bounded channel, so `ReadAsync` is cancellable and does not block the thread pool. This is fine for a personal tool with a handful of sessions.

## Alternatives considered

### System OpenSSH for terminals + .NET library for SFTP (rejected)

- A plain `Process` has no PTY. `SystemOpenSshProbeTests` runs `ssh -tt` through `Process`: the remote side gets a PTY (`/dev/pts/N`) but no window size, because `stty size` reports nothing. Without a local terminal, `ssh` cannot learn or change the size and sends no `window-change`.
- Getting resize requires hosting `ssh` inside a native pseudo-terminal: `forkpty`/`openpty` through P/Invoke on Linux and macOS, ConPTY on Windows 10 1809+. No first-party, maintained cross-platform .NET PTY package exists, so we would own native interop on three OSes.
- Host key verification would go through `known_hosts` files and text prompts on the PTY. There is no structured hook and errors come as stderr text. SFTP would still need a .NET library with its own authentication, host key handling and jump logic, so two transports would have to stay consistent. Connection reuse (`ControlMaster`) is not available on Windows.
- What it gives, full `ssh_config`, agent and ProxyJump support, matters only for alias import. That can be covered app-side for the subset we support.

### Microsoft.DevTunnels.Ssh (rejected)

A Microsoft SSH library (3.12.x, netstandard2.1). Its published API is channel-level and has no SFTP client, so it would still need a second library for files. It also has no typed `pty-req`/`window-change` helpers, and it targets dev tunnels rather than OpenSSH servers.

### Native libssh/libssh2 bindings or commercial libraries (rejected)

Native bindings need per-RID native binaries and their own interop surface. Commercial libraries (for example Rebex) add licensing. Neither gives anything SSH.NET lacks for this scope.

## Consequences

- A later switch to a native-PTY OpenSSH terminal stays possible behind `ITerminalSessionFactory` if `ssh_config` fidelity ever becomes a hard requirement.
- `SshNet.Agent` must be upgraded together with SSH.NET (its dependency range is `[2026.0.0, 2027.0.0)`).
- The Saved Connections task owns ssh_config parsing. The Host Key Verification task owns the known-hosts store behind `IHostKeyVerifier`.
