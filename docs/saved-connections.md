# Saved connections

## Model

A saved connection has a name, host, port, user, optional group (plain string), auth method (password, private key file, agent), optional key path, optional jump host (id of another saved connection), a read-only flag ([read-only-mode-and-confirmations.md](decisions/read-only-mode-and-confirmations.md)) and an AI access flag that makes the server visible to the AI bridge ([ai-bridge.md](decisions/ai-bridge.md)). Rows live in the SQLite `saved_connections` table.

Passwords are never stored in the database and never included in connection responses. They are written through `ICredentialStore` under a key derived from the connection id and removed on delete. The only way to read one back is `GET /api/connections/{id}/password`, used by the Show password button of the edit dialog; like every `/api` endpoint it needs the browser session and is served with `Cache-Control: no-store`. Private keys are referenced by path only.

## API

- `GET /api/connections`, `POST /api/connections`
- `GET|PUT|DELETE /api/connections/{id}`
- `GET /api/connections/{id}/password` returns `{ password }`; 404 when the connection does not exist or has no stored password
- `POST /api/connections/{id}/test`
- `POST /api/connections/import/ssh-config` imports the default user config (`~/.ssh/config`), no body
- `PUT|DELETE /api/connection-groups/{name}` rename a group or remove it (members become ungrouped)

Jump host chains that would form a cycle are rejected.

## Connection test

The test opens a terminal session through the Core SSH abstractions and closes it. Host keys go through the shared host key verification (`docs/decisions/host-key-verification.md`): an unknown or changed key opens the host key dialog. The decision must be made within the connection test timeout (15 seconds), otherwise the test reports a timeout and fails closed. The test never trusts a key on its own.

## ssh config import

Supported (exactly what is listed in [ssh-capabilities.md](ssh-capabilities.md)): `Host` blocks with `*`, `?` and `!` patterns, `Include` (nested up to 16 levels, wildcards in file names only), `HostName`, `User`, `Port`, `IdentityFile` (first value only), `ProxyJump` (a single hop that names another imported alias; `none` clears it).

Not supported and reported with file, line and affected aliases: `Match`, percent tokens, multi-value or invalid values, extra `IdentityFile` entries, multi-hop `ProxyJump`, and every other directive. Wildcard-only hosts are used for matching but are not imported as servers. Importing again updates connections previously imported from the same alias (host, port, user, key) instead of duplicating them; a name clash with another connection gets an ` (ssh config)` suffix. Imported hosts use a key file when `IdentityFile` is set, otherwise the agent.

## Known limitations

- One jump host level in the UI.
- Group is a label, groups have no settings of their own.
