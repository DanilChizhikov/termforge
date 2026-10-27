# Read-only mode and dangerous operation confirmations

## Decision

Every saved server has a `ReadOnly` flag (column `saved_connections.read_only`, migration `AddServerReadOnly`, default `false`). It is set in the add/edit server dialog or under Settings → Read-only servers, travels through settings export and import, and is enforced on the backend. Dangerous operations need a single-use confirmation token issued by the backend; a UI dialog alone is not enough.

## Read-only enforcement

`ServerWriteGate` (Core, `DTech.Core.Safety`) reads the flag through `IServerAccessModeReader` (a scoped repository lookup, so the singleton transfer queue can use it too). The services call it before they open any connection:

| Blocked operation | Service |
|---|---|
| Create folder, rename, delete | `RemoteFileOperations` |
| Upload (enqueue and retry) | `TransferEnqueuer` |
| Save and rollback | `RemoteFileSaveService` |
| Privileged (sudo) save and rollback | `SudoFileSaveService` |
| Docker start, stop, restart | `ContainerActionService` |
| Docker exec shell | `ContainerShellLauncher` |

A blocked request returns `423 Locked` with `code: ReadOnlyServer`. 403 is not used because the browser client retries a 403 once to refresh its CSRF token. Reading stays allowed: listing, downloads, opening files and backups, comparing, Docker logs, diagnostics and monitoring. The check sits in the services and not in the connection openers, so it cannot be bypassed by a different entry point (for example retrying an upload from the operation journal).

Interactive terminals are not blocked. A shell cannot be restricted reliably: any command typed into it runs with the SSH user's rights. The terminal therefore only shows a read-only banner (read-only intent). Use an SSH user without write rights, or server-side restrictions, when writes must be impossible.

Uploads already queued or running when the flag is turned on are not stopped; the flag applies to new requests.

## Dangerous operations

The central list is `DangerousOperation`:

| Operation | Target |
|---|---|
| `RemoteDelete` | remote path, as sent to `/delete` |
| `ContainerStop`, `ContainerRestart` | container name or id |
| `FileRollback` (normal and sudo) | file path |
| `TransferOverwrite` (upload with `overwrite: true`) | target directory |

Flow: after the user confirms in the dialog, the client calls `POST /api/confirmations` `{ operation, connectionId, target }` and receives `{ token, expiresAt, operation, serverName, target }`. The operation request then carries `confirmationToken`. `ConfirmationTokenStore` (singleton, in memory) binds the token to operation + server + target, keeps it for 2 minutes and removes it on first use, whether it matched or not. A missing, reused, expired or mismatched token returns `ConfirmationRequired` (428 for files and the editor, 409 for Docker, as before). Tokens are 32 random bytes; they are not persisted and do not survive a restart.

Order of checks: read-only first, then the token, so a read-only server never consumes a token. `POST /api/confirmations` returns 404 for an unknown server and 423 for a read-only one.

The former boolean flags (`confirm` on remote delete, `confirmed` on Docker actions) were replaced by the token. Local deletes, settings import replacement, secrets export and host key replacement keep their own explicit confirmations because they are not tied to a remote server.

## UI

- Sidebar rows show a caution `read-only` badge; the server name gets ", read-only" for screen readers.
- Remote pane: New folder, Rename, Delete and drops into the pane are disabled; the local pane's Upload button explains that the server is read-only.
- Editor: Save, Edit with sudo and Roll back are disabled with a notice; Docker: start/stop/restart and Shell are disabled, logs stay available.
- Confirmations name the server and the target: delete lists full paths and the server, rollback names the path and server, transfer overwrite names the target directory and server, Docker names container and server.
- The mockup's "Confirm dangerous operations" toggle is not implemented: PROJECT_SPEC §11 requires confirmations.

## Out of scope

Role-based access.
