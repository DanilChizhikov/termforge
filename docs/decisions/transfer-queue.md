# Transfer queue

## Decision

SFTP uploads and downloads are explicit operations executed by an in-process queue.

- Queue: `TransferQueue` keeps jobs in admission order behind a lock; one active job per target path. `TransferQueueHostedService` runs `Transfers:MaxParallelTransfers` workers (default 2, clamped to 1..8). Each job opens its own SFTP connection through `ITransferConnectionOpener`.
- Journal: every source item becomes one journal operation of kind `transfer.upload` or `transfer.download` with sanitized details (connection, source, target directory). States follow the journal state machine; failures carry a `retryable` flag.
- Planning: a file or a whole directory tree is planned first (directories, files, total bytes). Directories are created before files. A selected symlink is transferred as its target; symlinks inside a directory tree are skipped.
- Atomic write: each file is copied into a hidden partial `.<name>.<key>.part` next to the target and renamed over the target only after the copy completed. The key is a hash of direction, connection, source and target paths, source size and modification time, so a changed source never resumes a stale partial. Remote renames use `posix-rename@openssh.com` when available, otherwise delete-then-rename.
- Resume: retry (`POST /api/operations/{id}/retry`) replaces the failed job with a new one that appends to an existing partial if it is not larger than the source; files already committed by the previous attempt are skipped.
- Failures: connection loss, generic I/O failures and cancellation are retryable and keep the partial; permission and validation errors are permanent and delete the partial.
- Cancellation: cancelling a queued job removes it immediately; cancelling a running job signals its token and deletes the partial. `POST /api/transfers/{id}/discard` deletes a kept partial.
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

- The queue lives in memory: after a restart finished and failed transfers are visible only in the journal, and a kept partial is resumed only by retrying the journal entry.
- Owner, permissions and timestamps of the source are not copied.
- Directory synchronization is out of scope.
- Resume trusts the existing partial bytes; integrity is verified by the integration tests, not by a checksum at runtime.
