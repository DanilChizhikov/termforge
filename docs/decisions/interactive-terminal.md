# Interactive terminal

## Status

Accepted.

## Context

The workspace needs multiple independent interactive SSH terminals (PROJECT_SPEC section 6) on top of the existing transport (`ITerminalSessionFactory`, SSH.NET `ShellStream` with a PTY) and the local security model (browser session cookie, CSRF token, Origin check on every WebSocket).

## Decision

### Session manager

- `Core/Terminal/Sessions/TerminalSessionManager` is a singleton registry of open sessions. `TerminalLauncher` (scoped) resolves a saved connection into an `SshConnectionTarget` through `ConnectionTargetFactory`, so terminals use the same connect path, credentials, ProxyJump and host key verification (including the interactive host key dialog) as the connection test.
- Session ids are 32 bytes from `RandomNumberGenerator`, hex encoded. Every session stores its owner key, which is the browser session token; lookups, listing and closing compare it in constant time. A session created by one browser session is invisible (`404`) to any other.
- A session is attached to at most one WebSocket (`409` for a second one). A session that is not attached within 30 seconds after `POST /api/terminals` is closed, so an abandoned open never leaks an SSH connection.
- At most 32 sessions per browser session.
- Disposal is deterministic and idempotent. A session is closed and its SSH connection disposed when:
  - the user closes the tab (`DELETE /api/terminals/{id}`),
  - the browser closes or loses the WebSocket,
  - the remote shell exits or the SSH connection drops,
  - the application stops: `TerminalShutdownService` closes every session from `IHostApplicationLifetime.ApplicationStopping`, before Kestrel waits for open requests, and again in `StopAsync`. The manager also closes everything on container disposal.
- The browser always gets the reason (`TerminalCloseReasons`) in a final status message.

### API and protocol

- `GET /api/terminals`, `POST /api/terminals` `{ connectionId, columns, rows }`, `DELETE /api/terminals/{id}`. They are normal `/api` endpoints: session cookie, CSRF token for unsafe methods.
- `GET /api/terminals/{id}/ws` upgrades to a WebSocket. It passes through the same pipeline as `/api/ws` (Origin `403`, session `401`), then checks ownership and attachment.
- Binary frames carry raw terminal bytes in both directions.
- Text frames carry JSON control messages:
  - browser to server: `{ "type": "resize", "columns": 120, "rows": 40 }`, applied to the remote PTY with `window-change`;
  - server to browser: `{ "type": "status", "state": "connected" }`, `{ "type": "status", "state": "disconnected", "reason": "..." }`, `{ "type": "error", "message": "..." }` (for example an invalid size). Control messages are limited to 4 KiB.

### Backpressure

- Output: `ShellOutputPump` reads the shell stream into a bounded channel (64 chunks of up to 16 KiB). The relay awaits every WebSocket send before reading the next chunk, so a slow browser stops the pump once the channel is full instead of buffering without limit in TermForge.
- Input: the relay awaits each write to the shell before it receives the next WebSocket frame.

### Frontend

- `TerminalWorkspace` in the upper region: "New terminal" uses the selected server, tabs are grouped by server and labelled `<server> #<session number> (<state>)`, the disconnect reason is shown above the terminal.
- `TerminalView` owns one xterm.js instance, the fit addon and one WebSocket. A `ResizeObserver` refits when the panel size changes and sends a resize message when columns or rows change; hidden panels (zero size) are not refitted. Inactive tabs stay mounted, and the Terminals region keeps its content mounted while collapsed, so switching tabs or collapsing does not drop sessions.

## Consequences and limitations

- No automatic reconnect and no command replay: a lost session stays in the `disconnected` state. Reconnect policy and command history belong to the task "Reconnect Policy and Command History".
- Sessions do not survive a page reload; the old sessions close when their WebSockets drop.
- Each terminal opens its own SSH connection. A shared connection pool is a follow-up.
- SSH.NET adjusts the SSH channel window on its own, so output a remote process produces while the browser is stalled can still be buffered inside the library. TermForge's own buffers are bounded.
- tmux/screen attach is not implemented. Follow-up: an optional "attach to tmux session" command that starts the shell with `tmux new -A -s <name>` when `command -v tmux` succeeds.
