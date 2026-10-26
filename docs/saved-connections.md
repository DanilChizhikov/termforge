# Saved connections

## Model

A saved connection has a name, host, port, user, optional group (plain string), auth method (password, private key file, agent), optional key path and optional jump host (id of another saved connection). Rows live in the SQLite `saved_connections` table.

Passwords are never stored in the database or returned by the API. They are written through `ICredentialStore` under a key derived from the connection id and removed on delete. Private keys are referenced by path only.

## API

- `GET /api/connections`, `POST /api/connections`
- `GET|PUT|DELETE /api/connections/{id}`
- `POST /api/connections/{id}/test`
- `POST /api/connections/import/ssh-config` imports the default user config (`~/.ssh/config`), no body
- `PUT|DELETE /api/connection-groups/{name}` rename a group or remove it (members become ungrouped)

Jump host chains that would form a cycle are rejected.

## Connection test

The test opens a terminal session through the Core SSH abstractions and closes it. Host keys go through `IHostKeyVerifier`. Until the host key verification feature registers a real verifier, the default one rejects every unknown key and the test reports an actionable error. The test never trusts a key on its own.

## ssh config import

Supported (exactly what is listed in [ssh-capabilities.md](ssh-capabilities.md)): `Host` blocks with `*`, `?` and `!` patterns, `Include` (nested up to 16 levels, wildcards in file names only), `HostName`, `User`, `Port`, `IdentityFile` (first value only), `ProxyJump` (a single hop that names another imported alias; `none` clears it).

Not supported and reported with file, line and affected aliases: `Match`, percent tokens, multi-value or invalid values, extra `IdentityFile` entries, multi-hop `ProxyJump`, and every other directive. Wildcard-only hosts are used for matching but are not imported as servers. Importing again updates connections previously imported from the same alias (host, port, user, key) instead of duplicating them; a name clash with another connection gets an ` (ssh config)` suffix. Imported hosts use a key file when `IdentityFile` is set, otherwise the agent.

## Known limitations

- One jump host level in the UI.
- Group is a label, groups have no settings of their own.
