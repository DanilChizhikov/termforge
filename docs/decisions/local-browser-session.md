# Local browser session and host protection

## Context

The backend runs on the user's machine and controls SSH sessions. Loopback binding does not stop malicious websites (CSRF, DNS rebinding, cross-site WebSocket hijacking) or other local processes from sending requests to it. The browser must be authenticated without asking the user for a password on every launch.

## Decision

- Loopback only. The backend validates its configured bindings before Kestrel listens and refuses to start on any non-loopback address. Network access is a separate opt-in mode (`network-access.md`).
- Own Host allow-list middleware instead of the built-in `AllowedHosts` host filtering: the rule is "`localhost` or a loopback IP literal", independent of the port, and cannot be widened to `*` by configuration. `AllowedHosts` was removed from `appsettings.json` so there is a single rule.
- One-time bootstrap token per launch, delivered in the URL fragment of the browser the backend opens, exchanged with `POST /api/session/bootstrap` for an HttpOnly, `SameSite=Strict` session cookie. The fragment keeps the token out of server request logs, proxies and `Referer`. A tiny backend-served page (`/bootstrap`, `/bootstrap.js`, CSP `script-src 'self'`) performs the exchange so the SPA does not have to.
- Synchronizer CSRF token tied to the session, returned by `GET /api/session` and sent as `X-CSRF-Token`. A double-submit cookie was rejected because cookies are not isolated by port: any page on `127.0.0.1:<other port>` could read a non-HttpOnly cookie.
- `Origin` must equal the request origin exactly (scheme, host and port) whenever present, and is mandatory for WebSocket handshakes. `Sec-Fetch-Site` values other than `same-origin`/`none` are rejected, which also blocks same-site pages on other local ports.
- Secrets are 256-bit values from `RandomNumberGenerator`, compared with `CryptographicOperations.FixedTimeEquals`, kept only in memory and never logged.
- The security pipeline is a single `UseLocalHostSecurity()` call made first in `Program.cs`; endpoints under `/api` are protected without opting in.

## Consequences

- No password prompt for local use; a restart invalidates all sessions.
- Closing every browser window can drop the session cookie while the backend keeps running; the user restarts TermForge. Re-opening a session from a second launch is left for later.
- The bootstrap URL is never printed, so headless use and failed browser launches have no way in yet.
- Non-browser clients must send the session cookie and, for state-changing requests, the CSRF header; WebSocket clients must also send a matching `Origin`.
