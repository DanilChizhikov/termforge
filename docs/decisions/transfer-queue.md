# Transfer queue

## Decision

SFTP uploads and downloads are explicit operations executed by an in-process queue.

- Queue: `TransferQueue` reserves every target of a batch together; duplicate or busy targets reject the entire batch. Jobs become available to workers only after all journal entries exist. `TransferQueueHostedService` runs `Transfers:MaxParallelTransfers` workers (default 2, clamped to 1..8). Each job opens its own SFTP connection through `ITransferConnectionOpener`. The queue retains at most 1000 finished jobs without partial files and disposes cancellation sources after workers finish; jobs with kept partial files remain available for recovery.
- Journal: every source item becomes one journal operation of kind `transfer.upload` or `transfer.download` with sanitized details (connection, source, target directory). States follow the journal state machine; failures carry a `retryable` flag.
- Planning: a file or a whole directory tree is planned first (directories, files, total bytes). Directories are created before files. A selected symlink is transferred as its target; symlinks inside a directory tree are skipped.
- Atomic write: each file is copied into a hidden partial `.<name>.<key>.part` next to the target and renamed into place only after the copy completed. The key is a hash of direction, connection, source and target paths, source size and modification time, so a changed source never resumes a stale partial. Unconfirmed transfers use a non-replacing rename. Confirmed remote overwrites use `posix-rename@openssh.com` when available, otherwise move the original aside and restore it if replacement fails.
- Resume: retry (`POST /api/operations/{id}/retry`) replaces the failed job with a new one that appends to an existing partial if it is not larger than the source; files already committed by the previous attempt are skipped. Historical overwrite permission is never reused. A retry inspects targets again and returns 409 `TransferConflict` if they exist; retrying with `{ overwrite: true, confirmationToken }` requires a fresh one-time `TransferOverwrite` grant for uploads or `LocalTransferOverwrite` grant for downloads. Local overwrite grants also work for read-only servers because they do not change the server.
- Failures: connection loss, generic I/O failures and cancellation are retryable and keep the partial; permission and validation errors are permanent and delete the partial.
- Cancellation: cancelling a queued job removes it immediately; cancelling a running job signals its token and deletes the partial. `POST /api/transfers/{id}/discard` deletes a kept partial.
- Recovery: admission persists exact request metadata before publishing jobs, and each file checkpoints its partial path before any bytes are written. After restart interrupted operations become failed and their known partial paths appear in the panel and journal without starting a worker. Retry transfers cleanup ownership to the new operation; a changed source may leave an older partial, which remains tracked until explicitly discarded. A checkpoint failure prevents that file copy from starting.
- Cleanup: `hasPartial` identifies tracked partials even when their byte count is unknown or zero. Discard reserves the target against concurrent retry and deletes only recorded paths. Local download partials can be discarded while the SSH server is offline; remote cleanup requires a writable reachable server. Finished entries with pending cleanup are excluded from journal retention and panel clearing.
- Conflicts: `POST /api/transfers` inspects targets first and returns 409 `TransferConflict` with the existing paths; nothing is queued until the request is repeated with `overwrite: true`.
- Progress: snapshots (bytes, files, rate over a sliding window, ETA) are published on the shared `/api/ws` channel as `transfer.changed` every `Transfers:ProgressIntervalMilliseconds` (default 250); removals as `transfer.removed`.
- Shutdown: on application stop the queue closes for new jobs, running and queued jobs are cancelled and cleaned up within `Transfers:ShutdownTimeoutSeconds` (default 10).

## API

- `GET /api/transfers` current queue snapshots.
- `POST /api/transfers` `{ connectionId, direction: "upload" | "download", sourcePaths, targetDirectory, overwrite }` queues one transfer per source path.
- `POST /api/transfers/{id}/cancel`, `POST /api/transfers/{id}/discard`, `POST /api/transfers/clear` (drops finished jobs from the panel).

## UI

The transfer panel sits under the file panes. Upload/Download buttons send the pane selection to the folder open in the other pane; rows can be dragged between panes. Finished transfers refresh the receiving pane.

## Known limitations

- Work is not automatically resumed on restart. Old rows without exact recovery metadata cannot expose unknown partials for discard; no filesystem scan or path guessing is performed.
- A crash before a checkpoint completes cannot leave a newly written partial from that file, but a crash during commit may leave a recorded path whose file no longer exists; discard treats missing files as already cleaned.
- Owner, permissions and timestamps of the source are not copied.
- Directory synchronization is out of scope.
- Resume trusts the existing partial bytes; integrity is verified by the integration tests, not by a checksum at runtime.
