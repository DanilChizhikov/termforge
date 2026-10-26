# Docker management

## Decision

The Docker tab manages containers of the selected server through the Docker CLI over SSH. There is no Docker API socket forwarding and no agent on the server.

- Availability: `DockerAvailabilityChecker` reuses `DockerProbe` (`command -v docker`, `docker info`). The overview reports `Available`, `NotInstalled`, `NoAccess` (permission denied on the socket or daemon not running) or `Unknown` with the same actionable reason as the Diagnostics tab. Containers are listed only when Docker is available.
- Listing: `docker ps --all --no-trunc --format '{{json .}}'`, one JSON object per line, parsed by `DockerContainerListParser`. Warnings, malformed lines and entries whose id or name fails validation are skipped. The state comes from `State`, or from `Status` on older engines; health is read from the status suffix.
- Input safety: container ids and names must match `^[A-Za-z0-9][A-Za-z0-9_.-]{0,127}$` (`ContainerReference`) and are always single-quoted (`ShellArgument.Quote`) when put into a command. Actions and shells are fixed enums. Anything else is rejected with 400 before an SSH connection is opened.
- Actions: `start`, `stop`, `restart`. `stop` and `restart` require `confirmed: true`, otherwise 409 `ConfirmationRequired` and nothing runs. Every executed action is a journal operation of kind `docker.start`, `docker.stop` or `docker.restart` (server, container, action) that ends `succeeded` or `failed` with the classified reason. Docker actions are not retryable from the journal.
- Errors: `DockerFailureClassifier` maps command output to `DockerMissing` (exit 127), `PermissionDenied`, `DaemonUnavailable`, `ContainerNotFound` or `CommandFailed` with the sanitized daemon message.
- Logs: `docker logs --tail N [--follow] '<container>' 2>&1` runs through `IRemoteCommandStreamFactory`, a streaming SSH exec channel (`SshNetRemoteCommandStream`, reusing `ShellOutputPump`). The tail is clamped to 1..5000 (default 200). The stream is relayed over a dedicated WebSocket `GET /api/connections/{id}/docker/containers/{container}/logs/ws?tail=&follow=` as JSON messages `output`, `end` (exit code) and `error`. The socket is protected like every `/api/` endpoint (browser session, Origin, network access). Closing the socket or the page cancels the remote command and closes its SSH connection.
- Shell: `POST /api/connections/{id}/docker/containers/{container}/shell` opens a normal terminal session through `TerminalLauncher`/`TerminalSessionManager` and types ` printf '\033[H\033[2J'; exec docker exec -it '<container>' sh -c 'if command -v bash >/dev/null 2>&1; then exec bash; fi; exec sh'` into its PTY. SSH.NET exposes no public exec-with-PTY channel, so the login shell is replaced by `docker exec`; the leading space keeps the line out of shell history. The session is attached through the existing `/api/terminals/{id}/ws` bridge, bound to the browser session like any other terminal; `shell: "bash" | "sh"` forces one shell.

## API

- `GET /api/connections/{id}/docker` availability and containers.
- `POST /api/connections/{id}/docker/containers/{container}/{start|stop|restart}` `{ confirmed }`.
- `GET /api/connections/{id}/docker/containers/{container}/logs/ws` WebSocket log stream.
- `POST /api/connections/{id}/docker/containers/{container}/shell` `{ columns, rows, shell }` returns a terminal session.

## UI

The Docker tab shows the containers of the selected server with name, image, state (color, shape and word), ports and status, filters (all, running, problems) and refresh. Running containers offer Logs, Shell, Restart and Stop; stopped ones Logs and Start. Stop and Restart open a confirmation dialog with Cancel as the default. The panel below the table shows either logs (follow, last N lines, client-side line filter, stream state) or an xterm.js shell inside the container. The tab loads nothing until it is opened.

## Tests

- Unit tests (`Core.Tests/Docker`) use recorded `docker ps` JSON output, permission denied, daemon down, command not found and no such container outputs, injection attempts on container names, confirmation and journal recording.
- API tests (`Entrypoint.Tests/Docker`) cover the overview for missing Docker and permission denied, confirmation, journal entries, log streaming and cancellation over WebSocket, and the shell session.
- SSH integration tests (`Ssh.IntegrationTests/Docker`) run against the Alpine test server: streaming exec with exit code, prompt cancellation of an endless follow, and listing on a server without Docker.
- Docker-in-test-server integration tests are not included: running a Docker engine inside the SSH test container needs a privileged Docker-in-Docker container or mounting the host Docker socket, which gives the test server root on the CI host and is not available on the macOS and Windows runners. Real Docker commands are covered by the recorded outputs instead.

## Known limitations

- Images, volumes, networks and compose are out of scope.
- The container shell needs a POSIX login shell on the server (it types an `exec` line into it).
- Logs are buffered in the browser up to about one million characters; older lines are dropped.
- `--since` and timestamps are not offered; the line count is the only history limit.
- Docker actions are not retried from the journal.
