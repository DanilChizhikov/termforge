# Server diagnostics

Detects which remote features a saved connection supports and explains missing ones. Shown in the Diagnostics workspace tab ("Server diagnostics") for the selected server.

## API

- `GET /api/connections/{id}/capabilities` returns the cached report, running the checks if none is cached yet.
- `POST /api/connections/{id}/capabilities/refresh` runs the checks again and replaces the cached report.

Report: `state` (`Completed`, `ConnectionFailed`, `CredentialsUnavailable`), `checkedAt`, optional `message`, and `capabilities`: a list of `{ id, name, status, reason }` where `status` is `Available`, `Unavailable` or `Unknown`. Failed reports are not cached.

## Checks

Each check is an `ICapabilityProbe` registered in DI (no plugin framework). All commands of one report share one SSH connection through `IRemoteCommandRunner`.

- `exec`: command execution over an SSH exec channel.
- `system`: kernel (`uname -sm`), distribution (`/etc/os-release`), login shell (`$SHELL`).
- `sftp`: the SFTP subsystem opens.
- `docker.installed`, `docker.access`: `command -v docker`, then `docker info`. A permission error is reported as "the user is not in the docker group".
- `sudo`: `sudo -n true`; a password prompt is reported as unavailable.
- `utility.stat`, `utility.df`, `utility.free`, `utility.uptime`: `command -v`.

Other tasks (Docker actions, metrics) consume these results instead of probing again.

## Remote output is untrusted

- Every command runs through `sh -c` with a fixed script; remote text is never put into a command.
- Output is bounded (64 KiB per stream by default); exceeding it cancels the command and flags `OutputTruncated`. Each command has a timeout (10 s by default).
- Parsers are pure, ignore unknown lines, strip control characters and cap field lengths. Unrecognized output yields `Unknown`, never a guess.

## Limitations

- The cache lives in memory per application run and is not invalidated when a connection is edited; use Refresh.
- SFTP is probed with a separate connection.
- Transport feature support is limited to the exec channel and SFTP; other transports would add probes.
