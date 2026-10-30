# AI bridge

## Decision

TermForge hosts an MCP server (Model Context Protocol, Streamable HTTP, stateless) at `/mcp` on its own port. An AI agent such as Claude Code connects to it with a bearer token and works with the saved servers through TermForge: it runs commands, types into open terminals, manages Docker containers and reads and writes text files. The SDK is `ModelContextProtocol.AspNetCore`.

The bridge is off by default and every server has its own `AiAccess` flag (column `saved_connections.ai_access`, migration `AddServerAiAccess`, default `false`). An agent sees and touches only servers with the flag on.

## Access

- `/mcp` is outside `/api`, so the browser session cookie is not needed. `AiBridgeTokenMiddleware` guards it instead:
  - 403 for a client that is not on a loopback address, even in network access mode;
  - 404 while the bridge is off;
  - 401 with `WWW-Authenticate: Bearer` without a valid token.
- The token is 32 random bytes (base64url), generated in Settings → AI bridge. Only its SHA-256 hash is stored (`app_settings` key `ai_bridge.settings`), and it is compared in constant time. The UI shows the token once, together with the `claude mcp add` command. Generating a new token invalidates the old one.
- The CSRF check skips `/mcp`. The origin check still applies: a request with a foreign `Origin` header gets 403, so a web page cannot drive the bridge through the user's browser.
- The bridge settings and the token are not part of settings export. The `AiAccess` flag travels with each exported server.

## Tools

| Tool | Kind | Uses |
|---|---|---|
| `list_servers` | read | saved servers with AI access |
| `run_command` | write | SSH exec, 1–300 s timeout (60 by default), 256 KiB output limit |
| `list_terminals`, `read_terminal_output` | read | open terminals; the last 64 KiB of output kept in memory per session, escape sequences stripped |
| `send_to_terminal` | write | types into an open terminal, optionally with Enter |
| `docker_list_containers`, `docker_list_images`, `docker_container_logs` | read | the Docker services of the Docker tab; logs without follow, up to 5000 lines |
| `docker_container_action` | write | start, stop, restart, remove |
| `read_file` | read | the remote editor; returns the text and its `sha256` |
| `write_file` | write | the remote editor save: conflict check, backup, atomic replace, same mode and owner |

`write_file` needs the `sha256` from `read_file` for an existing file and refuses to overwrite a file it has not read. Without `expectedSha256` it only creates a new file.

## Approvals

Write tools wait for Approve or Deny in the browser. `AiApprovalBroker` keeps pending requests in memory and pushes them over the realtime channel (`aiBridge.approvalsChanged`). The browser answers with `POST /api/ai-bridge/approvals/{id}`. An unanswered request is denied after `AiBridge:ApprovalTimeoutSeconds` (120 by default, 10–900). Deny is the default button and Escape denies. A file write shows a diff of the current and the new content.

"Ask before AI actions" can be turned off in Settings. Write tools then run without asking. Reading never asks.

After approval the bridge issues the confirmation token that Docker stop, restart and remove need itself, so the user is not asked twice. `ServerWriteGate` still checks read-only mode.

## Read-only servers

On a read-only server the bridge refuses `run_command`, `send_to_terminal`, container actions and file writes before it asks the user. This is stricter than the browser, which keeps terminals open on read-only servers: a person sees what they type, an agent's commands are not visible until they run.

## Journal

`run_command` is recorded as `ai.command` and terminal input as `ai.terminal_input`, with the command text and the exit code. Command output is never stored. Docker actions and file saves are journaled by their own services as before (`docker.*`, `file.save`).
