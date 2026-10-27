# Workspace persistence

## Decision

The browser keeps the workspace in its existing stores and saves a snapshot to the backend; the backend validates it and stores it in SQLite. On the next start the snapshot is applied to the stores before the first render.

- What is saved: selected server, active view (Workspace, Editor, Docker, Diagnostics, History), terminal tabs (key, server id, server name) and the active one, local path, last remote path per server, panel layouts per group (`app-layout`, `main-workspace`, `file-panes`, sizes in percent, only after the user drags a separator), collapsed regions, open editor tabs and the active one, and unsaved editor content with the file version it was based on (mtime, size, SHA-256).
- Storage: `workspace_states` holds one row (`id = default`) with `schema_version` and the JSON document. Unsaved editor content lives in `workspace_editor_drafts` (primary key server id + path, cascade delete with the saved connection), so it survives a reset of the layout document. One save replaces both in a single transaction. Migration `AddWorkspaceState`.
- Saving: the browser subscribes to the stores and sends `PUT /api/workspace` 1 s after the last change. A snapshot equal to the last saved one is not sent. `flushWorkspaceState()` (frontend) sends a pending change at once and is also called on `pagehide`; `WorkspaceStateWriteCoordinator.FlushAsync` (backend) waits until a save in progress has been written. A later shutdown flow calls both.
- Autosave starts only after the saved workspace was fetched successfully, so a failed fetch never overwrites the stored workspace with an empty one.
- Validation: the backend rejects (400) unknown or malformed values: identifiers are `[A-Za-z0-9_-]{1,64}`, paths are 1–4096 characters without control characters, panel sizes are 0–100, and there are limits on tabs (100 terminal, 50 editor), remote paths (200), layouts and collapsed regions. A draft must belong to an open editor tab; one draft is at most 4 Mi characters, all drafts 16 Mi. References to deleted servers are dropped on save and on restore.

## Versioning and corrupted state

- `WorkspaceStateSchema.CurrentVersion` is 1. An older document is upgraded step by step by `IWorkspaceStateUpgradeStep` implementations registered in DI (one step per version, operating on the JSON object) before it is deserialized. The first format has no older versions, so no production step is registered yet; the pipeline is covered by tests.
- A document that is not valid JSON, does not match the schema, fails validation, is newer than the current version or has no upgrade path is deleted and an empty workspace is returned with a warning (`Corrupted` or `UnsupportedVersion`). Unsaved editor drafts are kept and their files reappear as editor tabs. The warning is returned once (the document is gone afterwards) and shown above the workspace until dismissed.

## Restore

- Terminal tabs come back disconnected with the server name, an offline status ring and "Reconnect". No SSH session is opened until the user clicks it; then a new shell is opened on that server. Live SSH processes do not survive a restart. There is no tmux/screen integration yet, so nothing is reattached.
- Editor tabs come back unopened ("Open file"), so starting TermForge does not connect to every server with an open file. A tab with unsaved changes keeps its unsaved marker. When the file is opened, the saved content is applied as the draft and a notice says so. If the file changed on the server since the draft was taken, the draft keeps the old version as the expected version, so saving goes through the existing conflict flow (compare, reload or overwrite) instead of silently overwriting.
- Selected server, local and remote paths are applied to the stores; the file panes then list those folders as usual.

## API

Protected like every `/api/` endpoint (browser session, CSRF for `PUT`, Origin and network access).

- `GET /api/workspace` → `{ state, drafts, warning }`, `warning` is `null` or `{ code, message }`.
- `PUT /api/workspace` `{ state, drafts }` → 204, or 400 with a problem detail.

## UI

No artboard draws the restore states. They reuse existing elements: the terminal status line (`terminal-restored`, styled like the disconnected line but neutral) with a secondary Reconnect button, the offline status ring for restored tabs, `notice notice-caution` for the reset warning and the recovered-draft notice, and a primary "Open file" button in the restored editor tab.

## Known limitations

- A change made less than a second before the browser tab is closed can be lost if the `pagehide` request does not reach the backend.
- Two browser tabs share one saved workspace; the last save wins.
