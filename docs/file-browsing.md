# File browsing

The lower region of the workspace has two panes: the local filesystem and the filesystem of the selected server (over SFTP). Each pane has roots (local only), Up, Refresh, breadcrumbs, sortable columns, single and multiple selection (Ctrl/Cmd click), New folder, Rename and Delete. Double click opens a directory. The current path of each pane is kept in UI state (one path per server for the remote pane). Transfers and search are separate tasks.

## API

All endpoints require the browser session; the POST endpoints also require the CSRF token (see `security.md`).

| Local | Remote (`{id}` is a saved connection id) | Purpose |
|---|---|---|
| `GET /api/files/local/roots` | | home directory and `/`, or the ready drives on Windows |
| `GET /api/files/local/list?path=` | `GET /api/files/remote/{id}/list?path=` | list a directory; without `path` the home directory is used |
| `GET /api/files/local/stat?path=` | `GET /api/files/remote/{id}/stat?path=` | metadata of one entry |
| `POST /api/files/local/directories` | `POST /api/files/remote/{id}/directories` | `{ parentPath, name }` |
| `POST /api/files/local/rename` | `POST /api/files/remote/{id}/rename` | `{ path, newName }`; `newName` is a plain name, an entry cannot be moved with it |
| `POST /api/files/local/delete` | `POST /api/files/remote/{id}/delete` | `{ path, confirm }`; without `confirm: true` the request is rejected |

Entries carry name, full path, kind (`File`, `Directory`, `SymbolicLink`, `Other`), size, UTC modification time, permissions (`rwxr-xr-x`, absent on Windows) and, for symbolic links, the kind of the target or a broken-link flag.

Errors are problem details with a `code`:

| Code | HTTP | Meaning |
|---|---|---|
| `Invalid` | 400 | empty, relative or traversal path, bad name, root rename/delete |
| `NotADirectory` | 400 | listing a file |
| `NotFound` | 404 | missing path or saved connection |
| `PermissionDenied` | 422 | the OS or the server refused access (422 and not 403 because the web client retries a 403 once to refresh the CSRF token) |
| `AlreadyExists`, `NotEmpty` | 409 | create or rename onto an existing name, delete of a non-empty directory |
| `ConfirmationRequired` | 428 | delete without `confirm: true`; nothing is connected or touched |
| `ConnectionFailed` | 502 | SSH connection, authentication or host key failure |
| `Cancelled`, `Failed` | 499, 500 | request cancelled, other failure |

## Path safety

- Local paths must be absolute. Control characters (including NUL), `\\?\` and `\\.\` device paths and any `..` segment (with `/` or `\` as separator) are rejected before the disk is touched. Redundant separators and `.` segments are normalised.
- Remote paths must start with `/`, contain no control characters and no `..` segment; they are normalised lexically. The server is never asked to resolve `~` or relative paths.
- Names for create and rename must not be empty, `.` or `..`, must not contain `/`, `\` or control characters and are limited to 255 characters.
- Local: rename and delete act on the link itself, so deleting a link to a directory leaves the target untouched. Remote: SSH.NET resolves links before rename and remove, which would act on the target, so renaming or deleting a remote symbolic link is refused with a clear error instead. Listing a link to a directory shows the target content. Broken links are shown as broken; a circular link cannot be listed and gives `Invalid` instead of hanging.

## Delete

Only files, symbolic links and empty directories are deleted; recursive delete is not offered. Confirmation is required by the API itself, not only by the dialog: the UI sends `confirm: true` only after the user presses "Confirm delete". Deleting a filesystem root is refused.

## Remote connections

Every remote request opens its own SSH connection through the common connector (host key verification included, so an unknown key pauses the request until the user decides in the dialog) and closes it afterwards. There is no shared connection pool yet; it is a follow-up and would make navigation faster.

## Limitations

- No transfers (upload/download), no search, no recursive delete, no move between directories.
- Symbolic link target paths are not shown, only the kind of the target (SSH.NET does not expose `readlink`).
- Remote listings are not paged; very large directories are returned at once.
- Permissions are shown read-only; owner and group names are not resolved.
