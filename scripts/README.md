# Scripts

---
script: run.sh

description: Builds the frontend and the backend and starts TermForge on `http://127.0.0.1:5023`; the backend opens the browser with a one-time launch link. Dev mode runs the backend in the background and the Vite dev server with hot reload on `http://127.0.0.1:5173`.

Options:
- `-d` dev mode: backend in background + Vite dev server; open `http://127.0.0.1:5173` in the browser the backend opened
- `-s` skip the frontend build and serve the existing `src/Api/Entrypoint/wwwroot`
- `-c` force `npm ci` even if `node_modules` is up to date
- `-h` show usage
- `-- <args>` pass the remaining arguments to the backend (e.g. `--Lifecycle:OpenBrowser=false`, `--Persistence:DatabasePath=/tmp/tf.db`, `--Kestrel:Endpoints:Http:Url=http://127.0.0.1:5199` for another port; `--urls` is ignored because `appsettings.json` defines Kestrel endpoints; dev mode expects the default port)

example:

```sh
./scripts/run.sh
./scripts/run.sh -d
./scripts/run.sh -s -- --Lifecycle:OpenBrowser=false
```
___

---
script: publish.sh

description: Builds the frontend once, then publishes a self-contained TermForge for each runtime identifier into `publish/termforge-<version>-<rid>` and packs it into `.zip` (win-*) or `.tar.gz`. Fails when the bundled frontend is missing from a build.

Options:
- `-r` runtime identifier, repeatable (default: `linux-x64`, `linux-arm64`, `win-x64`, `win-arm64`, `osx-x64`, `osx-arm64`)
- `-v` version (default: `<Version>` from `Directory.Build.props`)
- `-b` build number (default: `0`)
- `-o` output directory (default: `publish/` in the repository root)
- `-s` skip the frontend build and use the existing `src/Api/Entrypoint/wwwroot`
- `-n` do not create archives
- `-h` show usage

> [!IMPORTANT]
> Deletes and recreates `<output>/termforge-<version>-<rid>` and the matching archive for every runtime it publishes.

example:

```sh
./scripts/publish.sh
./scripts/publish.sh -r osx-arm64 -n
./scripts/publish.sh -v 1.0.0 -b 42
```
___

---
script: smoke-test.sh

description: Starts a published TermForge with a temporary database, captures the one-time launch link through a fake browser command, creates a browser session, checks `GET /api/session` and the bundled frontend, prints the active credential store, requests exit through `POST /api/lifecycle/exit` and expects exit code 0.

Options:
- `-p` port on `127.0.0.1` (default: `5099`)
- `-t` timeout in seconds for startup and for shutdown (default: `60`)
- `-h` show usage

example:

```sh
./scripts/smoke-test.sh publish/termforge-1.0.0-osx-arm64
./scripts/smoke-test.sh -p 5199 -t 120 publish/termforge-1.0.0-linux-x64
```
___
