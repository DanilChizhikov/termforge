# Remote file editor

## Decision

Remote files open in the Editor workspace tab. Each open file is an inner tab; one file per server path. The editor is Monaco (`monaco-editor` + `@monaco-editor/react`).

- Monaco is chosen over CodeMirror because it ships a diff editor, syntax highlighting for shell, YAML, JSON, INI-style configs and Dockerfiles, and familiar keyboard behavior out of the box.
- Monaco is bundled locally through Vite: `loader.config({ monaco })` replaces the default CDN loader, only the needed language definitions are imported (`editor.api` instead of the full bundle) and workers are built with `?worker`. The app makes no external requests.
- The editor and diff components are loaded with `React.lazy`, so Monaco is downloaded only when the first file is opened.
- The Monaco theme in `src/Web/src/components/editor/editorTheme.ts` mirrors `tokens.css`, the same way `terminalTheme.ts` does; change both together.

## Open

`GET /api/editor/remote/{id}/file?path=` returns the text, a byte order mark flag, permissions and the version: UTC mtime, size and SHA-256 of the content.

- Only regular files open. Symbolic links, directories and other kinds are rejected (`NotAFile`); the user opens the target instead, so the editor never writes through a link.
- Size limit `Editor:MaxFileBytes` (default 2 MiB) is checked on the attributes and again while reading (`TooLarge`).
- Binary detection: a NUL byte or invalid UTF-8 rejects the file (`Binary`). A UTF-8 BOM is kept and written back.

## Save

`PUT /api/editor/remote/{id}/file` with `{ path, content, hasByteOrderMark, expectedVersion }`.

1. The current file is read again and its version compared with `expectedVersion`. Any difference in mtime, size or hash returns `409` with code `Conflict`, the current version and current content; nothing is written. A missing file with an expected version, or an existing file without one, is also a conflict.
2. The current file is copied to a hidden sibling backup `.<name>.termforge-<UTC timestamp>.bak` with the original mode. If that fails the save stops and the original is untouched.
3. The new content is written to a hidden temporary sibling `.<name>.termforge-<guid>.tmp` (created `0600`, then given the original mode). If the session user is not the owner, ownership is copied with `chown`.
4. The temporary file atomically replaces the original with the SFTP `posix-rename@openssh.com` extension. When the server does not support it, the original is renamed aside, the temporary file is renamed into place and the aside copy is deleted; a failure in between renames the original back.
5. If the owner cannot be preserved (typical for files owned by another user in a writable directory), the content is written in place instead, so owner and mode stay as they were, and the response carries a warning. The backup from step 2 still exists.
6. Old backups beyond `Editor:MaxBackupsPerFile` (default 10) are deleted, newest kept.

Any failure before the replace removes the temporary file and reports that the original was not changed. The response returns the new version, the backup name, the strategy (`AtomicRename`, `RenameFallback`, `InPlace`) and warnings.

## Conflicts in the UI

A conflict pauses saving and shows the server change with three choices: Compare (side by side diff of server copy and the user's version), Reload from server (drops the user's edits) and Overwrite, which needs a second confirmation and then saves with the server's current version as the expected version. Nothing is overwritten silently.

## Backups and rollback

- `GET /api/editor/remote/{id}/backups?path=` lists backups of a file, newest first.
- `GET /api/editor/remote/{id}/backups/content?path=&name=` reads one backup for comparison.
- `POST /api/editor/remote/{id}/rollback` with `{ path, backupName, expectedVersion }` restores a backup through the same save pipeline, so the replaced content is backed up too and conflicts are detected. Backup names are validated against the file name, so a request cannot point at another file.

## Journal

Every save and rollback is an operation (`file.save`, `file.rollback`) with server, path, strategy, size, SHA-256, backup name and warnings. Conflicts and failures are recorded as failed operations.

## Known limitations

- Only UTF-8 text; other encodings are rejected as binary.
- The version uses the SFTP mtime, which has one second resolution; the content hash covers changes within the same second.
- There is no lock on the remote file: a change between the version check and the rename is not detected.
- Backups live next to the file and need write access to its directory; read-only directories cannot be saved (sudo editing is a separate task).
- Ownership can be preserved only when the session user may `chown`; otherwise the file is written in place after a backup.
- Merging the server change into the user's version is manual through the diff view.
