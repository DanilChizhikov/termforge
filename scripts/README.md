# Scripts

---
script: run.sh

description: Builds the frontend and the backend and starts TermForge on `http://127.0.0.1:5023`; the backend opens a private launch file in the browser. Dev mode starts Vite first, then bootstraps the browser directly on `http://127.0.0.1:5173` with hot reload.

Options:

- `-d` dev mode: backend in background + Vite dev server; the browser opens and authenticates directly on `http://127.0.0.1:5173`
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

---
script: publish.sh

description: Builds the frontend once, then publishes a self-contained single-file TermForge for each runtime identifier into `publish/termforge-<version>-<rid>` and packs it into `.zip` (win-*) or `.tar.gz`. Native libraries extract at runtime; `wwwroot` and configuration remain external. Fails when the bundled frontend is missing from a build.

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

---
script: smoke-test.sh

description: Starts a published single-file TermForge with a temporary database, captures the private launch file through a fake browser command, exchanges its token and checks that the file is deleted. Checks authenticated and cookie-only `GET /api/session`, the bundled frontend, credential status and the executable's credential helper protocol without accessing a credential, then requests exit through `POST /api/lifecycle/exit` and expects exit code 0.

Options:

- `-p` port on `127.0.0.1` (default: `5099`)
- `-t` timeout in seconds for startup and for shutdown (default: `60`)
- `-h` show usage

example:

```sh
./scripts/smoke-test.sh publish/termforge-1.0.0-osx-arm64
./scripts/smoke-test.sh -p 5199 -t 120 publish/termforge-1.0.0-linux-x64
```

---
script: project-status.sh

description: Sets the Status field of repository issues in a GitHub Project by option name; field and option IDs are resolved at runtime. Used by agents when taking a task and opening its PR, and by the `issue-status` workflow.

Options:

- `-i` issue number (required, repeatable)
- `-s` status option name, e.g. `In Progress`, `Review`, `Done` (required)
- `-o` project owner; defaults to `DanilChizhikov`
- `-p` project number; defaults to `32`
- `-R` issue repository; defaults to `DanilChizhikov/quant-vision`
- `-h` show usage

Env:

- `GH_TOKEN` token with the `project` scope; defaults to the local `gh` login

example:

```sh
./scripts/project-status.sh -i 5 -s 'In Progress'
./scripts/project-status.sh -i 5 -i 6 -s Review
```

---
