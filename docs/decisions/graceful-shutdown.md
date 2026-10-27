# Exit warnings and graceful shutdown

## Decision

Closing a browser tab never stops TermForge. The backend stops only after an explicit exit from the UI (`Exit app` in the header), `Ctrl+C`/`SIGTERM`, or the optional idle shutdown.

### Pending attention

`GET /api/lifecycle/attention` returns what an exit would affect:

- `transfers`: queued and running transfers (same shape as `GET /api/transfers`).
- `unsavedFiles`: unsaved editor content. The browser reports it through workspace persistence (unsaved drafts are saved with the workspace); the exit dialog calls `flushWorkspaceState()` first so the backend sees the latest drafts. Drafts of deleted servers are skipped.
- `terminalSessions`: open terminal sessions of every browser session, without their session ids (the id is a capability for attaching to the terminal).
- `state`: `running`, `waitingForTransfers` or `shuttingDown`; `idleShutdownMinutes` lets the dialog describe what closing the tab does.

### Exit

- `POST /api/lifecycle/exit` with `{ "waitForTransfers": false }` (or no body) starts the shutdown at once; active transfers are cancelled. Returns 202 `{ state }`; 409 `ExitInProgress` when already exiting.
- `{ "waitForTransfers": true }` with active transfers enters `waitingForTransfers`: new work is rejected, queued and running transfers finish, then the shutdown starts. Without active transfers it shuts down at once.
- `DELETE /api/lifecycle/exit` cancels a pending wait (204) and the application accepts work again; 409 `NoPendingExit` otherwise.
- While exiting, unsafe `/api` requests return 503 with code `ApplicationExiting`, except `/api/lifecycle`, `/api/session` and `/api/workspace` (the browser still flushes its workspace).

The dialog follows the `Exit` artboard: the list of pending items, a note that closing the tab does not stop anything, `Exit now` (danger link), `Cancel` and the primary `Exit after transfers finish` (only `Cancel` and `Exit` when no transfer is active). While waiting it shows live progress with `Exit now` and `Keep working`; after the backend stops it says so and the tab can be closed. `Review` on an unsaved file opens it in the editor.

### Ordered shutdown

`GracefulShutdown` runs registered `IShutdownStep`s in `ShutdownStage` order, once, bounded by `Lifecycle:ShutdownTimeoutSeconds` for all steps together:

1. `StopAcceptingWork`: the work admission gate closes.
2. `CancelTransfers`: the transfer queue closes and every transfer is cancelled; queued ones are journaled as cancelled at once.
3. `CloseSessions`: `TerminalShutdownService` closes every terminal session.
4. `FlushJournal`: waits until the transfer workers stop, which is when running transfers have written their cancelled or final state to the journal and removed partial files.
5. `FlushWorkspace`: `WorkspaceStateWriteCoordinator.FlushAsync` waits for a workspace save in progress.

Then `IHostApplicationLifetime.StopApplication()` stops the host, which disposes resources. A step that fails is logged and the next one runs; when the timeout elapses the remaining steps are skipped with a warning and the host stops anyway.

The same sequence runs from `ApplicationStopping` (`GracefulShutdownHostedService`), so `Ctrl+C` and `SIGTERM` shut down in the same order. The former per-feature `ApplicationStopping` hooks of terminals and transfers were replaced by steps; their `StopAsync` fallbacks remain.

### Idle shutdown

`Lifecycle:IdleShutdownMinutes` (default `0`, off). When set, TermForge stops after that many minutes with no browser connected to `/api/ws` (every open TermForge tab keeps this socket) and no active transfer. It is checked every `Lifecycle:IdleCheckIntervalMilliseconds` (default 15000).

## Known limitations

- Unsaved editor content is known to the backend only through the saved workspace, so with several tabs open the last saved tab wins.
- There is no system tray integration; the browser is the only UI for exiting.
- Terminal sessions are closed, not detached: there is no tmux/screen integration.
