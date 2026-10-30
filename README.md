# TermForge

TermForge is a local web console for your own VPS and home servers. It runs on your computer, opens in your browser and talks to the servers over SSH. In one window you get terminals, a local and a remote file manager, a remote file editor, Docker containers, server diagnostics and a journal of everything it did.

Nothing runs on the servers except plain SSH, SFTP and shell commands. TermForge listens on `127.0.0.1` only, unless you set up network access yourself.

## Contents

- [Install](#install)
- [Start and stop](#start-and-stop)
- [Use](#use)
- [Configuration](#configuration)
- [Security](#security)
- [Troubleshooting](#troubleshooting)
- [Build from source](#build-from-source)
- [CI and releases](#ci-and-releases)

## Install

Download the archive for your system from the GitHub releases page, unpack it and run `TermForge` (`TermForge.exe` on Windows). The archive includes the .NET runtime and the UI.

| System | Archive | Status |
|---|---|---|
| macOS, Apple silicon | `osx-arm64` | verified |
| macOS, Intel | `osx-x64` | verified under Rosetta 2 |
| Linux x64, glibc | `linux-x64` | verified |
| Linux arm64, glibc | `linux-arm64` | verified |
| Windows x64 | `win-x64` | verified in CI |
| Windows arm64 | `win-arm64` | built, not verified |

```sh
tar -xzf termforge-1.0.0-osx-arm64.tar.gz
xattr -dr com.apple.quarantine termforge-1.0.0-osx-arm64
./termforge-1.0.0-osx-arm64/TermForge
```

macOS builds are not notarized, so the `xattr` line is needed for a downloaded copy. Linux needs ICU (`libicu`); `libsecret` is optional and lets TermForge keep passwords in the system keyring. Details for each system, data locations, updates and removal are in [docs/installation.md](docs/installation.md). The checks behind the "verified" column are in [docs/release-verification.md](docs/release-verification.md).

## Start and stop

TermForge starts a local server on `http://127.0.0.1:5023` and opens your default browser with a one-time launch link. That link creates the browser session. Keep the terminal window open, since the server runs there and writes its log to it. Closing the browser tab does not stop TermForge.

To stop it, press Exit app in the header or `Ctrl+C` in the terminal. If transfers are running, files are unsaved or terminals are open, the exit dialog lists them and lets you exit now, wait for the transfers or cancel. Shutdown then cancels the remaining transfers, closes the sessions and saves the journal and the workspace. See [docs/decisions/graceful-shutdown.md](docs/decisions/graceful-shutdown.md).

The launch link works once. If the browser loses the session, start TermForge again. Only one TermForge can use the port at a time.

## Use

### Servers

Add a server with the Add server button in the sidebar, or import hosts from `~/.ssh/config`. A server has a host, port, user and either a private key path, an SSH agent or a password. Passwords go to the credential store, never to the database. Test on a server row opens a session and reports the result. Servers can carry a group label and can be marked read-only. The filter above the list (⌘K or Ctrl+K) matches names, hosts, users and groups; the collapsed sidebar keeps a rail of server initials with their status. See [docs/saved-connections.md](docs/saved-connections.md).

On the first connection TermForge shows the server's `SHA256:` host key fingerprint and asks you to accept it once, accept and save it, or reject it. Keys already in `~/.ssh/known_hosts` are trusted without a prompt; TermForge reads that file and never writes to it. A changed key blocks the connection until you confirm the new one separately. See [docs/decisions/host-key-verification.md](docs/decisions/host-key-verification.md).

### Terminals

The Terminals & files tab holds xterm.js terminals grouped by server. New session opens an SSH shell on the selected server, and you can open as many as you need. Each tab shows its state and the reason when it disconnects. Per server you choose how a dropped connection behaves: a Reconnect button, automatic reconnect with backoff, or nothing. A reconnect always opens a new shell and never replays commands.

The History panel next to the terminal keeps commands with their time and exit code, read from shell integration sequences. Terminal output is never stored. You can pin, search and insert commands. See [docs/reconnect-and-command-history.md](docs/reconnect-and-command-history.md).

Terminal recording is off by default. Once you turn it on in Settings, Record in a terminal's status line saves that session's output (never keystrokes) as an asciicast file that you can replay and delete in Settings. Recordings may contain secrets, and TermForge warns about that. See [docs/decisions/terminal-recording.md](docs/decisions/terminal-recording.md).

### Files and transfers

Below the terminals sit a local pane and a remote SFTP pane with breadcrumbs, sorting, selection, new folder, rename and delete with confirmation. The search button in each pane finds file names by glob (`*`, `?`) below the current folder and streams results as it goes. See [docs/file-browsing.md](docs/file-browsing.md) and [docs/decisions/filename-search.md](docs/decisions/filename-search.md).

Upload and Download, or dragging between the panes, put files and folders into the transfer queue. The panel under the panes shows progress, rate and remaining time, and offers Cancel, Retry and Discard partial. Data goes to a hidden `.part` file that is renamed into place when complete, so a failed transfer never leaves a half-written target. A retry continues from the `.part` file. An existing target is replaced only after you choose Overwrite. See [docs/decisions/transfer-queue.md](docs/decisions/transfer-queue.md).

### Editing remote files

Double-click a remote file to open it in the editor tab (Monaco, several files at once, `⌘S` or `Ctrl+S` to save, diff view). Before saving, TermForge checks that the file has not changed on the server since you opened it. If it has, you get Compare, Reload from server, or Overwrite after a second confirmation. Each save backs up the original, writes a temporary file and renames it into place with the same permissions. Backups can be compared and rolled back. See [docs/decisions/remote-file-editor.md](docs/decisions/remote-file-editor.md).

Files that your SSH user cannot read or write open only after you pick Edit with sudo or Open with sudo. TermForge asks for the sudo password for each elevated action, sends it to `sudo -S` over standard input and keeps owner, group and mode on save. It does not offer arbitrary root commands. See [docs/decisions/privileged-sudo-editing.md](docs/decisions/privileged-sudo-editing.md).

### Docker

The Docker tab lists the containers of the selected server with state, ports and status. You can start, stop and restart them (stop and restart ask first), follow logs and open a shell inside a running container. If Docker is missing or your user has no access to it, the tab says so. See [docs/decisions/docker-management.md](docs/decisions/docker-management.md).

### Diagnostics and monitoring

The Diagnostics tab checks the selected server for command execution, SFTP, Docker, sudo and common utilities, and explains what is missing. The sidebar and the Diagnostics tab also show connection latency, uptime, load average, CPU with a 15-minute chart, memory and free disk space, refreshed every 10, 30 or 60 seconds while visible. See [docs/diagnostics.md](docs/diagnostics.md) and [docs/decisions/server-monitoring.md](docs/decisions/server-monitoring.md).

### Journal

The Journal tab lists transfers, saves, rollbacks, Docker actions and other operations with live status, filters and details. Retry is available for operations that can be repeated. See [docs/decisions/operation-journal.md](docs/decisions/operation-journal.md).

### Workspace

TermForge saves the workspace about a second after it changes and restores it on the next start: the selected server, the active tab, terminal tabs, local and remote paths, panel sizes, open editor tabs and unsaved editor changes. Restored terminals start disconnected with a Reconnect button, because SSH sessions do not survive a restart. See [docs/decisions/workspace-persistence.md](docs/decisions/workspace-persistence.md).

### Read-only servers

A read-only server rejects uploads, deletes, renames, saves, rollbacks and Docker actions in the backend. Terminals stay available with a read-only banner, since a shell cannot be fully restricted. Risky actions on any server (remote delete, Docker stop and restart, rollback, overwriting upload) need a confirmation first. See [docs/decisions/read-only-mode-and-confirmations.md](docs/decisions/read-only-mode-and-confirmations.md).

### AI bridge

TermForge can act as an MCP server for AI agents such as Claude Code. Turn it on in Settings → AI bridge, generate a token and run the command shown there, for example:

```sh
claude mcp add --transport http termforge http://127.0.0.1:5023/mcp --header "Authorization: Bearer <token>"
```

The agent sees only servers with AI access turned on in the server dialog. It can run commands, type into open terminals and read their output, manage Docker containers and read and write text files. Every change waits for your approval in the browser unless you turn "Ask before AI actions" off. Read-only servers refuse all changes. The bridge accepts local connections only. See [docs/decisions/ai-bridge.md](docs/decisions/ai-bridge.md).

### Import and export

Settings → Import & export writes servers with their saved passwords, preferences, the workspace layout and pinned commands to one `.tfsettings` file encrypted with PBKDF2-SHA256 and AES-256-GCM. Every export generates a new key that is shown once; the import asks for the file and that key. Private keys are never exported, only their paths. Import shows every change before applying it and lets you merge or replace. See [docs/decisions/settings-import-and-export.md](docs/decisions/settings-import-and-export.md).

### Updates

At start TermForge checks the releases of this repository and shows a notice when a newer version exists. It never downloads or installs anything. To update, unpack the new archive and start it; your data stays where it was.

## Configuration

Settings live in `appsettings.json` next to the binary. Any key can also be passed on the command line as `--Section:Key=value` or as an environment variable `Section__Key`.

| Key | Default | Meaning |
|---|---|---|
| `Kestrel:Endpoints:Http:Url` | `http://127.0.0.1:5023` | address and port. Non-loopback addresses are refused unless network access is enabled |
| `Lifecycle:OpenBrowser` | `true` | open the browser after start |
| `Lifecycle:BrowserCommand` | none | executable that receives the launch link instead of the default browser |
| `Lifecycle:ShutdownTimeoutSeconds` | `10` | limit for the graceful shutdown |
| `Lifecycle:IdleShutdownMinutes` | `0` (off) | stop after this many minutes with no open tab and no running transfer |
| `Persistence:DatabasePath` | see [installation](docs/installation.md#where-data-lives) | SQLite database file |
| `Credentials:VaultPath` | `credentials.vault` in the data folder | encrypted vault file |
| `HostKeys:DecisionTimeout` | `00:02:00` | how long an unanswered host key prompt waits before the connection fails |
| `Transfers:MaxParallelTransfers` | `2` (max 8) | transfers that run at the same time |
| `Editor:MaxFileBytes` | `2097152` | largest file the editor opens |
| `Editor:MaxBackupsPerFile` | `10` | backups kept per edited file |
| `Updates:CheckOnStartup` | `true` | check GitHub releases at start |
| `AiBridge:ApprovalTimeoutSeconds` | `120` (10–900) | how long an AI action waits for approval before it is denied |
| `NetworkAccess:Enabled` | `false` | allow access from other devices, see [docs/security.md](docs/security.md#network-access-mode) |

Example: `./TermForge --Kestrel:Endpoints:Http:Url=http://127.0.0.1:5199 --Lifecycle:OpenBrowser=false`.

Network access from other devices needs a password hash from `TermForge --hash-password`, the allowed host names and an HTTPS endpoint. Without them TermForge refuses to start.

## Security

The browser session uses an HttpOnly `SameSite=Strict` cookie, a CSRF token on every state-changing request, exact `Origin` checks, a host header allow-list against DNS rebinding and origin checks on WebSockets. Passwords live in Windows Credential Manager, the macOS Keychain or the Linux Secret Service. Without a usable system store they go to an encrypted vault file (PBKDF2-SHA256, AES-256-GCM) that you unlock with a master password. Logs never contain secrets, launch tokens or cookies.

The threat model and the browser contract are in [docs/security.md](docs/security.md), credential storage in [docs/credentials.md](docs/credentials.md), and supported SSH features (key types, agents, ProxyJump, what is missing) in [docs/ssh-capabilities.md](docs/ssh-capabilities.md).

## Troubleshooting

Common problems (port in use, the browser did not open, missing ICU or libsecret, host key changed, Docker access) and their fixes are in [docs/troubleshooting.md](docs/troubleshooting.md).

## Build from source

### Requirements

- .NET SDK 10.0.110 or a newer feature band (see `global.json`)
- Node.js 22 or newer, for the UI. `dotnet build` and `dotnet test` do not need it.
- Docker, only for the SSH integration tests

### Build and test

```sh
dotnet build termforge.slnx
dotnet test termforge.slnx
```

UI, from `src/Web`:

```sh
npm ci
npm run lint
npm run typecheck
npm test
npm run build          # writes the UI into src/Api/Entrypoint/wwwroot
```

`src/Tests/Ssh.IntegrationTests` starts disposable OpenSSH servers in Docker through Testcontainers, so it needs a Docker engine that runs Linux containers. The agent tests also need `ssh-agent` and `ssh-add` on `PATH`, and the system ssh probe needs `ssh`. Without Docker these tests are reported as inconclusive with the reason. `TERMFORGE_SKIP_DOCKER_TESTS=true` skips them on purpose, as CI does on macOS and Windows.

The OS credential store tests run against the real store of the machine. Where it is unusable, for example on a headless Linux runner without Secret Service, they are reported as inconclusive with the reason.

### Run from source

```sh
./scripts/run.sh       # builds the UI and the backend, then starts TermForge
./scripts/run.sh -d    # backend plus the Vite dev server with hot reload on http://127.0.0.1:5173
```

Without the script: build the UI, then `dotnet run --project src/Api/Entrypoint`. Without a built UI, `/` answers with a short hint and the API still works. For UI work run `npm run dev` in `src/Web` while the backend runs, and open `http://127.0.0.1:5173` in the browser the backend opened. Use `127.0.0.1`, not `localhost`, so the session cookie is shared.

### Publish

```sh
./scripts/publish.sh                    # all six runtimes into publish/, with archives
./scripts/publish.sh -r linux-x64 -n    # one runtime, no archive
./scripts/smoke-test.sh publish/termforge-1.0.0-linux-x64
```

`publish.sh` builds the UI once and publishes a self-contained TermForge per runtime identifier. `smoke-test.sh` starts a published build, creates a browser session through the launch link, checks the API and the UI, then exits through the API and expects exit code 0. Both scripts are described in [scripts/README.md](scripts/README.md).

### Database migrations

TermForge uses SQLite through EF Core and applies migrations at start. To add one (the tool is pinned in `.config/dotnet-tools.json`):

```sh
dotnet tool restore
dotnet tool run dotnet-ef migrations add <Name> --project src/Infrastructure/Persistence.Migrations --startup-project src/Infrastructure/Persistence.Migrations --context TermForgeDbContext --output-dir Migrations --namespace DTech.Persistence.Migrations
```

Remove the `// <auto-generated />` and `/// <inheritdoc />` lines from the generated files. The model snapshot stays in `Migrations/` next to the migrations. See [docs/decisions/sqlite-persistence.md](docs/decisions/sqlite-persistence.md).

### Layout

```
src/
  Api/
    Entrypoint/                 ASP.NET Core host, lifecycle, HTTP and WebSocket endpoints
    Core/                       domain and application logic, one folder per feature
  Infrastructure/
    Ssh/                        SSH transport for terminals, commands and SFTP
    HostKeys/                   known_hosts parsing and host key trust
    Persistence/                SQLite storage
    Persistence.Migrations/     database migrations
    Credentials/                OS credential stores and the encrypted vault
  Web/                          React + TypeScript UI (Vite)
  Tests/                        MSTest projects, one per backend project, plus Ssh.IntegrationTests
docs/                           user and design documentation, decisions in docs/decisions/
scripts/                        helper scripts, described in scripts/README.md
```

## CI and releases

- `ci` builds and tests the solution on Linux, macOS and Windows, checks the UI, and publishes and smoke-tests the app on each of the three systems. It runs for every push and pull request to `develop` and `master`.
- `release-start` (run by hand) bumps `<Version>` in `Directory.Build.props`, pushes `release/X.Y.Z` from `develop` or `hotfix/X.Y.Z` from `master`, and opens a pull request to `master`. It needs the `RELEASE_TOKEN` secret, a token with `contents` and `pull-requests` write access.
- `release` runs when that pull request is merged. It publishes the six archives with `scripts/publish.sh`, smoke-tests the native ones, creates the GitHub release `X.Y.Z` and merges `master` back into `develop`.
- `pr-labeler` labels pull requests by branch prefix (`feature/`, `bugfix/`, `hotfix/`, `release/`). The labels group the release notes.

## License

MIT, see [LICENSE](LICENSE).
