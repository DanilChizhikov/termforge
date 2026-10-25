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
