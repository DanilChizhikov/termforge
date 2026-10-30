# Docker management

## Decision

The Docker tab manages containers, compose projects and images of the selected server through the Docker CLI over SSH. There is no Docker API socket forwarding and no agent on the server.

- Availability: `DockerAvailabilityChecker` reuses `DockerProbe` (`command -v docker`, `docker info`). The overview reports `Available`, `NotInstalled`, `NoAccess` (permission denied on the socket or daemon not running) or `Unknown` with the same actionable reason as the Diagnostics tab. Containers are listed only when Docker is available.
- Listing: `docker ps --all --no-trunc --format '{{json .}}'`, one JSON object per line, parsed by `DockerContainerListParser`. Warnings, malformed lines and entries whose id or name fails validation are skipped. The state comes from `State`, or from `Status` on older engines; health is read from the status suffix.
- Input safety: container ids and names must match `^[A-Za-z0-9][A-Za-z0-9_.-]{0,127}$` (`ContainerReference`), image references `^[A-Za-z0-9][A-Za-z0-9._/:@-]{0,254}$` without `..` (`ImageReference`), compose project names `^[a-z0-9][a-z0-9_-]{0,127}$` (`ComposeProjectName`). They are always single-quoted (`ShellArgument.Quote`) when put into a command. Actions and shells are fixed enums. Anything else is rejected with 400 before an SSH connection is opened.
- Actions: `start`, `stop`, `restart`, `remove` (`docker rm -f`). `stop`, `restart` and `remove` need a confirmation token (`ContainerStop`, `ContainerRestart`, `ContainerRemove`, see [read-only-mode-and-confirmations.md](read-only-mode-and-confirmations.md)), otherwise 409 `ConfirmationRequired` and nothing runs. Every executed action is a journal operation of kind `docker.start`, `docker.stop`, `docker.restart` or `docker.remove` (server, container, action) that ends `succeeded` or `failed` with the classified reason. Docker actions are not retryable from the journal.
- Compose: the project comes from the `com.docker.compose.project` label of the listed containers (`ComposeLabelParser`). `restart` runs `docker compose -p '<project>' restart`; `recreate` runs `down` and `up -d` with the working directory and config files taken from the compose labels. Both need a confirmation token (`ComposeRestart`, `ComposeRecreate`) and are journaled as `docker.compose.restart` and `docker.compose.recreate`. A project without containers on the server returns `ComposeProjectNotFound`; a server without Compose v2 returns `ComposeMissing`.
- Images: `docker image ls --all --no-trunc --format '{{json .}}'`, parsed by `DockerImageListParser`. Remove runs `docker rmi '<image>'` with an `ImageRemove` confirmation token and is journaled as `docker.image.remove`. Pull streams `docker pull '<image>' 2>&1` over a WebSocket like the logs and is journaled as `docker.pull`; it is a write, so a read-only server refuses it.
- Errors: `DockerFailureClassifier` maps command output to `DockerMissing` (exit 127), `PermissionDenied`, `DaemonUnavailable`, `ContainerNotFound`, `ImageNotFound`, `ImageInUse`, `ComposeMissing` or `CommandFailed` with the sanitized daemon message.
- Logs: `docker logs --tail N [--follow] '<container>' 2>&1` runs through `IRemoteCommandStreamFactory`, a streaming SSH exec channel (`SshNetRemoteCommandStream`, reusing `ShellOutputPump`). The tail is clamped to 1..5000 (default 200). The stream is relayed over a dedicated WebSocket `GET /api/connections/{id}/docker/containers/{container}/logs/ws?tail=&follow=` as JSON messages `output`, `end` (exit code) and `error`. The socket is protected like every `/api/` endpoint (browser session, Origin, network access). Closing the socket or the page cancels the remote command and closes its SSH connection.
- Shell: `POST /api/connections/{id}/docker/containers/{container}/shell` opens a normal terminal session through `TerminalLauncher`/`TerminalSessionManager` and types ` printf '\033[H\033[2J'; exec docker exec -it '<container>' sh -c 'if command -v bash >/dev/null 2>&1; then exec bash; fi; exec sh'` into its PTY. SSH.NET exposes no public exec-with-PTY channel, so the login shell is replaced by `docker exec`; the leading space keeps the line out of shell history. The session is attached through the existing `/api/terminals/{id}/ws` bridge, bound to the browser session like any other terminal; `shell: "bash" | "sh"` forces one shell.

## API

- `GET /api/connections/{id}/docker` availability and containers.
- `POST /api/connections/{id}/docker/containers/{container}/{start|stop|restart|remove}` `{ confirmationToken }`.
- `GET /api/connections/{id}/docker/containers/{container}/logs/ws` WebSocket log stream.
- `POST /api/connections/{id}/docker/containers/{container}/shell` `{ columns, rows, shell }` returns a terminal session.
- `POST /api/connections/{id}/docker/compose/{project}/{restart|recreate}` `{ confirmationToken }`.
- `GET /api/connections/{id}/docker/images` image list.
- `POST /api/connections/{id}/docker/images/remove` `{ image, confirmationToken }`.
- `GET /api/connections/{id}/docker/images/pull/ws?image=` WebSocket pull progress.

## UI

The Docker tab has Containers and Images views. Containers shows name, image, state (color, shape and word), ports and uptime, filters (all, running, problems) and refresh. Containers of one compose project are grouped under a header with the working directory and Restart and Recreate. Running containers offer Logs, Shell, Restart, Stop and Remove; stopped ones Logs, Start and Remove. Images shows repository, tag, id, size, creation time and the containers that use it, with Remove and a Pull form that streams the pull output. Rows of both views can be selected with checkboxes; the bar above the table runs Start, Restart, Stop or Remove (containers) or Remove (images) on the selection one by one and reports what failed. Stop, Restart, Remove and the compose actions open a confirmation dialog with Cancel as the default. Remove dialogs offer "Don't ask again", stored per browser in `localStorage` and turned back on in Settings → Docker confirmations; stop, restart and compose actions always ask. The backend requires a confirmation token either way. The panel below the containers table shows either logs (follow, last N lines, client-side line filter, stream state) or an xterm.js shell inside the container. The tab loads nothing until it is opened.

## Tests

- Unit tests (`Core.Tests/Docker`) use recorded `docker ps` and `docker image ls` JSON output, compose labels, permission denied, daemon down, command not found, no such container or image and image in use outputs, injection attempts on container, image and project names, confirmation and journal recording.
- API tests (`Entrypoint.Tests/Docker`) cover the overview for missing Docker and permission denied, confirmation, journal entries, compose actions, image listing, removal and pull, log streaming and cancellation over WebSocket, and the shell session.
- SSH integration tests (`Ssh.IntegrationTests/Docker`) run against the Alpine test server: streaming exec with exit code, prompt cancellation of an endless follow, and listing on a server without Docker.
- Docker-in-test-server integration tests are not included: running a Docker engine inside the SSH test container needs a privileged Docker-in-Docker container or mounting the host Docker socket, which gives the test server root on the CI host. Real Docker commands are covered by the recorded outputs instead.

## Known limitations

- Volumes, networks, image build and push, and compose projects that have no containers on the server are out of scope.
- The container shell needs a POSIX login shell on the server (it types an `exec` line into it).
- Logs are buffered in the browser up to about one million characters; older lines are dropped.
- `--since` and timestamps are not offered; the line count is the only history limit.
- Docker actions are not retried from the journal.
