# Reconnect policy and command history

## Reconnect policy

Each saved server has a reconnect policy (server dialog, "If the connection drops"):

| Mode | Behavior |
|---|---|
| Manual (default) | A disconnected tab shows a Reconnect button. One attempt, immediately. |
| Automatic | A tab that lost a live session reconnects by itself with exponential backoff: the wait before attempt N is `first wait * 2^(N-1)`, capped by `longest wait`. After the attempt limit (1 to 20) it stops and shows the last error. |
| Off | No reconnect action. |

Rules:

- Reconnect opens a brand-new shell through the same path as "New terminal" (`POST /api/terminals/reconnect`). Nothing typed or run in the previous shell is ever replayed, and a command that was running is not restarted. The previous screen contents are not kept.
- Only transient failures are retried (unreachable host, channel or session errors). Authentication failures, host key problems and missing credentials stop the sequence immediately.
- A first connection that failed is never retried automatically, even in Automatic mode; use the Reconnect button.
- The backoff scheduler (`Core/Reconnect/ReconnectScheduler`) takes an injected `TimeProvider`.
- Closing the tab while an automatic reconnect is waiting abandons the result: the shell opened afterwards is closed again.

## Command history

The History panel next to the terminal lists commands for the selected server: pinned first, then recent. Each row can be inserted into the terminal (no newline: you press Enter yourself), pinned or deleted. Search is a substring match. "Clear unpinned" keeps pinned commands. At most 1000 unpinned commands are kept per server.

Stored per command: text, UTC timestamp, exit code when known, pinned flag. Terminal output is never stored or sent to the history code.

### Detection and its limits

TermForge does not guess commands from keystrokes or screen contents. It reads standard shell integration escape sequences that the shell prints into the terminal stream:

- `OSC 633;E;<command>` carries the command line (VS Code convention; `\\` and `\xHH` escapes, `;` is escaped as `\x3b`).
- `OSC 133;D;<exit code>` (and `633;D`) marks the end of a command and carries the exit code.

Consequences:

- Without shell integration nothing is recorded. The panel says so.
- Integration is opt-in. The panel buttons "bash" / "zsh" insert a one-line snippet into the terminal without Enter; you review it and press Enter. It is not added to any startup file, so it applies to that shell session only. Add it to `~/.bashrc` or `~/.zshrc` yourself for permanent capture. Servers that already emit these sequences (for example with their own VS Code style integration) need nothing.
- Only bash and zsh snippets are provided. fish, sh and others record nothing unless they emit the sequences themselves.
- bash reports a command after it finished, so its timestamp is the finish time; zsh reports it before it starts. A command still running when the session ends is stored without an exit code.
- Multi-line commands are stored but cannot be inserted back from the panel.
- Commands over 2000 characters or containing control characters are not stored.

### Secret exclusion

A command is not stored when:

- it starts with a space or tab (with bash `HISTCONTROL=ignorespace` such commands also never reach the snippet),
- it matches an exclusion pattern. Defaults cover words like password, token, secret, api key, `export *KEY=` style assignments, `NAME_TOKEN=value` prefixes, `Authorization: Bearer`, credentials in URLs and `mysql -pSECRET`.

Patterns are .NET regular expressions with a 100 ms timeout; a timeout counts as a match. They are configurable through `GET/PUT/DELETE /api/command-history/exclusions` (PUT replaces the whole list, DELETE restores defaults); there is no UI for this yet. Pattern matching is best effort: it cannot recognize every secret, so use a leading space for anything sensitive.

### API

- `GET /api/connections/{id}/history?search=&limit=`, `DELETE /api/connections/{id}/history`
- `PUT /api/command-history/{id}/pin` `{ "pinned": true }`, `DELETE /api/command-history/{id}`
- `GET /api/command-history/integration/{bash|zsh}`
- The terminal WebSocket sends `{ "type": "history" }` after a command was stored, so the panel refreshes.

Deleting a server deletes its history. Tables: `command_history` and four `reconnect_*` columns on `saved_connections` (migration `AddReconnectPolicyAndCommandHistory`).
