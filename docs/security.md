# Security

TermForge is a local web application: an ASP.NET Core backend on the user's machine and a browser UI. The backend holds SSH sessions, credentials and file access, so anything that can talk to it can act as the user on their servers. Listening on loopback alone does not protect it: every website open in the user's browser and every process on the machine can reach `127.0.0.1`.

This document describes the threat model, the protections implemented in `src/Api/Entrypoint/Security` and `src/Api/Entrypoint/Lifecycle`, and the contract the browser UI must follow.

## Threat model

### Assets

- Live SSH terminal sessions and SFTP access to the user's servers.
- Saved connections, credentials and keys reachable through the backend.
- The ability to stop the application.

### Trust boundaries

- The user and the browser tab opened by TermForge are trusted.
- Other websites open in the same browser are untrusted.
- Other users and other processes on the same machine are untrusted unless they already run as the same OS user (see limitations).
- The local network is untrusted. By default the backend refuses to start with a non-loopback binding. Network access mode is an explicit opt-in that requires a password (see [Network access mode](#network-access-mode)).

### Threats and mitigations

| Threat | Attack | Mitigation |
|---|---|---|
| Remote access from the network | Connecting to the backend from another machine | Unless `NetworkAccess:Enabled` is `true`, binding validation requires every effective listening address to be loopback. `0.0.0.0`, `*`, `+`, `[::]`, LAN addresses, host names other than `localhost`, Unix sockets and `http_ports`/`https_ports` (which bind to all interfaces) are rejected before Kestrel starts listening. Validation follows Kestrel precedence: `Kestrel:Endpoints` are always checked; `urls` are checked when no endpoint is configured or `preferHostingUrls` is set; `http_ports`/`https_ports` only when `urls` is empty as well. Addresses Kestrel ignores (e.g. `ASPNETCORE_HTTP_PORTS=8080` preset in .NET container images) do not block startup |
| DNS rebinding | A malicious site points its own domain at `127.0.0.1` and reads responses as same-origin | Host header allow-list: only `localhost` and loopback IP literals (`127.0.0.0/8`, `::1`) are accepted, plus the names listed in `NetworkAccess:AllowedHosts` when network access mode is enabled. A rebinding request carries the attacker's domain in `Host` and gets `400` |
| Cross-site request forgery | A malicious site submits a form or `fetch` to `http://127.0.0.1:5023/api/...` | Session cookie is `SameSite=Strict`; state-changing requests require the `X-CSRF-Token` header that only same-origin script can obtain; `Origin` must match exactly |
| Cross-site WebSocket hijacking | A malicious site opens `ws://127.0.0.1:5023/...`; browsers do not apply CORS to WebSockets | Every WebSocket handshake must carry an `Origin` equal to the backend origin, a valid session cookie and an origin proof in `Sec-WebSocket-Protocol` |
| Other local web apps | A local service on another port receives the shared host cookie and replays it to TermForge | Every protected API request requires the origin proof in addition to the cookie. The proof lives in origin-scoped `sessionStorage` and is issued only by successful bootstrap or password login; cookie-only `/api/session` gets `403` |
| Clickjacking | Framing TermForge in a hostile page | `X-Frame-Options: DENY`, `frame-ancestors 'none'` on the bootstrap page; the Strict cookie is not sent in a cross-site frame |
| Other local processes | A process sends HTTP requests to the port without a browser | Every protected `/api` request and WebSocket needs the session cookie and origin proof. The 256-bit bootstrap token is single use, expires after 60 seconds, and is carried in a private launch file rather than process arguments |
| Token leakage | Bootstrap token in server logs, proxy logs, `Referer`, or browser history | The token travels in the URL fragment (never sent to the server), the bootstrap page clears it with `history.replaceState`, sends `Referrer-Policy: no-referrer`, and the token is exchanged with a `POST` body. Tests run with `Trace` logging and assert that no token or cookie value appears in any log message |
| Timing attacks | Guessing secrets byte by byte | All secret comparisons use `CryptographicOperations.FixedTimeEquals`; sessions are looked up by the SHA-256 of the token |
| Password guessing (network mode) | Brute force against `POST /api/session/login` | PBKDF2-SHA256 hash (600 000 iterations by default), per-client and global attempt limits with lockout, `429` with `Retry-After` |
| AI bridge misuse | A local process or another device calls the MCP endpoint `/mcp`, or a web page drives it through the user's browser | `/mcp` answers only loopback clients (`403` otherwise, also in network access mode), only while the bridge is turned on (`404` otherwise) and only with the bearer token, stored as a SHA-256 hash and compared in constant time (`401`). A foreign `Origin` gets `403`. The agent sees only servers with AI access turned on, read-only servers refuse writes, and by default every write waits for Approve or Deny in the browser. See [decisions/ai-bridge.md](decisions/ai-bridge.md) |
| Saved password disclosure | Reading a server password back through the API | `GET /api/connections/{id}/password` is a normal `/api` endpoint: it needs the browser session, the `Origin` check applies and the response is `Cache-Control: no-store`. Connection lists and details never contain passwords |
| Eavesdropping (network mode) | Sniffing the password or session cookie on the LAN | HTTPS is required for every non-loopback binding; plain HTTP needs `NetworkAccess:AllowInsecureHttp=true` and is reported at `Critical` level on every start and in the sign-in form |

### Known limitations

- A process running as the same OS user can read the browser profile, attach a debugger or replace the binary. Protecting against that is out of scope for a local application.
- The session cookie is a browser session cookie for the current launch, while the origin proof is kept in the bootstrapped tab's `sessionStorage`. Reload preserves it; a new tab without the proof must sign in again in network mode or restart TermForge to bootstrap a new session. Cookie-only proof recovery is intentionally unavailable.
- When opening the browser fails (no default browser, headless machine) or `Lifecycle:OpenBrowser` is `false`, the bootstrap URL is not shown anywhere, by design, because it must not appear in logs.
- Plain HTTP on loopback: the cookie is marked `Secure` only when the request is HTTPS.
- Network access mode: see the limitations in [Network access mode](#network-access-mode).
- Read-only servers block uploads, deletes, renames, saves, privileged writes and Docker actions on the backend, but a terminal on such a server is only marked read-only: commands typed into the shell still run with the SSH user's rights. See [Read-only servers and confirmations](#read-only-servers-and-confirmations).

## Browser contract

This is the contract the frontend (`src/Web`) must follow.

### Launch and bootstrap

1. After Kestrel starts, immediately before browser launch, the backend generates a random 256-bit bootstrap token (base64url, 43 characters), valid for 60 seconds.
2. The backend writes a redirect to a private temporary HTML file and opens that filename in the default browser. Unix directories use `0700` and files `0600`; Windows uses a current-user-only directory ACL under local application data. Browser process arguments contain only the filename. The file redirects to:

   ```
   http://127.0.0.1:5023/bootstrap#token=<bootstrap-token>
   ```

   The origin is the first loopback address the server is listening on (`Kestrel:Endpoints:Http:Url`, default `http://127.0.0.1:5023`).
3. `GET /bootstrap` serves a minimal page and `GET /bootstrap.js` a script. The script reads the token from the fragment, removes it from the address bar and calls:

   ```
   POST /api/session/bootstrap
   Content-Type: application/json

   {"token":"<bootstrap-token>"}
   ```

   - `200 {"csrfToken":"<origin-proof>"}` with `Set-Cookie: termforge_session=<session-token>; path=/; samesite=strict; httponly` on the first valid exchange.
   - `403 Forbidden` for an invalid, expired or consumed token: the token works exactly once per launch.
4. The script saves the proof as `termforge.origin-proof` in `sessionStorage` and navigates to `/`. A tokenless bootstrap page can resume only with both the cookie and that stored proof. The private launch file is removed after consumption, expiration or graceful shutdown.

The initial navigation from the private file is cross-site. Only `GET /bootstrap` without an `Origin` and with `Sec-Fetch-Mode: navigate` plus `Sec-Fetch-Dest: document` may bypass the fetch-site check. This does not exempt token exchanges, API requests, script fetches, frames or WebSockets.

The SPA does not need to implement any of the bootstrap steps.

### Session cookie

| Property | Value |
|---|---|
| Name | `termforge_session` |
| Flags | `HttpOnly`, `SameSite=Strict`, `Path=/`, `Secure` only over HTTPS, no `Domain`, no expiry (browser session) |
| Lifetime | Current backend launch; a restart invalidates it |

The SPA cannot and must not read it. `fetch` sends it automatically for same-origin requests (`credentials: 'same-origin'`, the default).

### CSRF token

- Obtain it only from a successful `POST /api/session/bootstrap` or `POST /api/session/login` response. Keep it in `sessionStorage`, never in `localStorage`, cookies or URL queries.
- Send it as `X-CSRF-Token` on every protected API request, including `GET /api/session`, downloads and streaming responses. Without it, or with a wrong value, the backend answers `403`.
- `GET /api/session` validates both credentials and returns the same proof; it cannot establish or refresh one from a cookie alone. The client does not refresh proofs or retry rejected requests automatically.
- It stays valid for the whole session.

### Requests

- All API endpoints live under `/api`; the only exception is the AI bridge at `/mcp`, which uses a bearer token instead of the session cookie (see [Request pipeline](#request-pipeline)). Every `/api` request except `POST /api/session/bootstrap` and `GET`/`POST /api/session/login` requires the session cookie; without it the backend answers `401`. On `401` the UI asks `GET /api/session/login`: when password sign-in is enabled it shows the sign-in form, otherwise it tells the user to restart TermForge.
- Use relative URLs (same origin). The backend rejects any request whose `Origin` header differs from its own origin (`403`) and any request the browser marks with `Sec-Fetch-Site` other than `same-origin` or `none` (`403`).
- The `Host` must be `localhost`, a loopback IP or, in network access mode, one of `NetworkAccess:AllowedHosts` (`400` otherwise).
- The SPA shell and static assets outside `/api` are served without a session: they must not contain secrets.

### WebSocket

- Connect to a same-origin URL built from `window.location`, with `termforge.<origin-proof>` as the requested subprotocol. The server selects that same protocol. Never include the proof in the URL.
- Browsers send `Origin` and the session cookie on the handshake automatically. Missing or foreign `Origin` → `403`, missing or invalid session → `401`, missing or invalid proof protocol → `403`.
- Terminal sockets `/api/terminals/{id}/ws` additionally require that the terminal id (256 random bits) was created by the same browser session (`404` otherwise) and is not already attached (`409`). A leaked id alone is useless without the session cookie and a same-origin handshake.

### Exit

- `POST /api/lifecycle/exit` with the session cookie and `X-CSRF-Token` returns `202 Accepted`; the backend stops after the response is sent. Closing a browser tab does not stop the backend.

### Development proxy

A dev server (e.g. Vite) works without backend changes:

- Run it on `127.0.0.1` (Vite: `server.host: '127.0.0.1'`), not `localhost`, because cookies are scoped to the host name, not the port.
- Proxy `/api` (including WebSockets), `/bootstrap` and `/bootstrap.js` to `http://127.0.0.1:5023`, keeping the original `Host` header (`changeOrigin: false`). The backend then sees `Host: 127.0.0.1:5173` and `Origin: http://127.0.0.1:5173`, which match.
- `scripts/run.sh -d` starts Vite before the backend and sets `Lifecycle:BrowserOrigin=http://127.0.0.1:5173`, so bootstrap stores the proof at the dev origin. Manually moving from port 5023 to 5173 does not transfer `sessionStorage`.

## Request pipeline

`UseLocalHostSecurity()` must be the first middleware call after `builder.Build()` so it guards everything added later (static files, SPA fallback, WebSockets):

1. Host header validation → `400`.
2. Security headers (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Cache-Control: no-store` for `/api`).
3. `UseWebSockets()`. Endpoints must not call it again.
4. Origin and `Sec-Fetch-Site` validation → `403`.
5. Browser session: required for `/api/*` (except the bootstrap exchange and `/api/session/login`) and every WebSocket → `401`.
6. Origin proof: required for every protected `/api` request, every browser WebSocket and every state-changing request outside `/api` (except the bootstrap exchange, the password sign-in and `/mcp`) → `403`.
7. AI bridge token for `/mcp`: non-loopback client or `Host` → `403`, bridge turned off → `404`, missing or wrong bearer token → `401`.
8. Bootstrap, session and sign-in endpoints.

New endpoints are protected automatically when they are mapped under `/api`. Endpoints that read the session can use `context.Features.Get<BrowserSession>()`.

## Lifecycle

- Startup: binding validation runs in an `IStartupFilter`, before Kestrel binds. Without network access mode a non-loopback binding throws `InsecureBindingException`; with network access mode an incomplete or insecure configuration throws `NetworkAccessConfigurationException`. In both cases the process exits without listening.
- Browser: `BrowserOpeningService` opens a private redirect file after the application has started. `Lifecycle:OpenBrowser=false` skips it (tests, headless runs); `Lifecycle:BrowserCommand` sets an executable that receives the filename as its only argument instead of the OS default browser (`open` on macOS, `xdg-open` on Linux, shell execute on Windows). Launch failures are logged without secrets.
- Running: the backend does not depend on browser tabs.
- Exit: the exit endpoint, `Ctrl+C` or `SIGTERM` trigger `IHostApplicationLifetime.StopApplication`. Hosted services and request handlers receive cancellation through their `CancellationToken`/`ApplicationStopping`; the host waits up to `Lifecycle:ShutdownTimeoutSeconds` (default 10) for them, then the process exits.

## Configuration

| Key | Default | Meaning |
|---|---|---|
| `Kestrel:Endpoints:Http:Url` | `http://127.0.0.1:5023` | Listening address; must be loopback unless network access mode is enabled |
| `NetworkAccess:*` | disabled | See [Network access mode](#network-access-mode) |
| `Lifecycle:OpenBrowser` | `true` | Open the browser after start |
| `Lifecycle:BrowserCommand` | not set | Executable used instead of the default browser |
| `Lifecycle:BrowserOrigin` | not set | Optional loopback HTTP origin for bootstrap, used by the development proxy |
| `Lifecycle:ShutdownTimeoutSeconds` | `10` | Graceful shutdown timeout, must be positive |

## Network access mode

Disabled by default. It lets other devices (for example a laptop on the home LAN) use TermForge after signing in with a password. It is a single-user mode: everyone who knows the password acts as the OS user running TermForge. The design is recorded in `docs/decisions/network-access.md`.

### Enabling

1. Generate a password hash. The password is read without echo (or as one line from standard input when it is redirected) and must be at least 12 characters long:

   ```sh
   ./TermForge --hash-password
   dotnet run --project src/Api/Entrypoint -- --hash-password
   ```

   The output looks like `pbkdf2-sha256$600000$<salt>$<hash>`. Only the hash is stored. Quote it in shells because it contains `$`.
2. Configure an HTTPS endpoint with a certificate, the hash and the host names other devices use, for example in `appsettings.json` next to the executable or through environment variables (`NetworkAccess__PasswordHash`, `NetworkAccess__AllowedHosts__0`):

   ```json
   {
     "Kestrel": {
       "Endpoints": {
         "Http": { "Url": "http://127.0.0.1:5023" },
         "Lan": {
           "Url": "https://0.0.0.0:5443",
           "Certificate": { "Path": "termforge.pfx", "Password": "<pfx password>" }
         }
       }
     },
     "NetworkAccess": {
       "Enabled": true,
       "PasswordHash": "pbkdf2-sha256$600000$...",
       "AllowedHosts": ["termforge.lan", "192.168.1.10"]
     }
   }
   ```

3. Open `https://termforge.lan:5443` on the other device and sign in.

### Startup refusal

With `NetworkAccess:Enabled=true` the backend refuses to start, before listening, when:

- `NetworkAccess:PasswordHash` is missing, malformed or uses fewer than 100 000 iterations. This applies even if every binding is loopback.
- `NetworkAccess:AllowedHosts` is empty or contains something that is not a plain host name or IP address (no scheme, port or `*`).
- A non-loopback binding uses plain HTTP and `NetworkAccess:AllowInsecureHttp` is not `true`.
- One of the limits below is not positive.

With `NetworkAccess:AllowInsecureHttp=true` it starts, logs a `Critical` "INSECURE" message for every plain HTTP network binding on every start, and the sign-in form shows a warning when it is opened over HTTP from a non-loopback host.

### Sign-in contract

| Request | Session | CSRF | Result |
|---|---|---|---|
| `GET /api/session/login` | no | no | `200 {"passwordLoginEnabled":true,"signedInWithPassword":false}`; `signedInWithPassword` is `true` when the request carries a password session |
| `POST /api/session/login` `{"password":"..."}` | no | no | `200 {"csrfToken":"<origin-proof>"}` with the session cookie; `401` wrong password; `429` with `Retry-After` (seconds) when locked out; `404` when network access mode is disabled |
| `POST /api/session/logout` | yes | yes | `204`, the session is removed and the cookie deleted |

- `Origin` and `Sec-Fetch-Site` are validated for the sign-in like for every other request.
- A password session uses the same cookie (`termforge_session`, `HttpOnly`, `SameSite=Strict`, `Secure` over HTTPS) and the same CSRF and WebSocket rules as the launch session. It expires after `NetworkAccess:SessionLifetimeHours` regardless of activity. Up to 32 sessions are kept; the oldest is dropped first.
- Every attempt is counted against the client IP address and against a global counter before the password is checked, so parallel requests cannot bypass the limit; a successful sign-in resets both. After `MaxFailedLoginAttempts` attempts from one address, or `GlobalMaxFailedLoginAttempts` attempts in total, sign-in is refused for `LockoutMinutes`, even with the correct password. Failures older than `LockoutMinutes` are forgotten.
- The local launch link (`/bootstrap`) keeps working on loopback in network mode.
- Every `/api` endpoint and WebSocket, including terminals, file browsing and server diagnostics, requires a session; the SPA shell and static assets do not contain secrets and are served without one so the sign-in form can load.

### Configuration

| Key | Default | Meaning |
|---|---|---|
| `NetworkAccess:Enabled` | `false` | Allow non-loopback bindings and password sign-in |
| `NetworkAccess:PasswordHash` | not set | Output of `TermForge --hash-password` |
| `NetworkAccess:AllowedHosts` | empty | Host names and IP addresses accepted in the `Host` header besides loopback |
| `NetworkAccess:AllowInsecureHttp` | `false` | Accept plain HTTP network bindings (not recommended) |
| `NetworkAccess:MaxFailedLoginAttempts` | `5` | Attempts per client address before lockout |
| `NetworkAccess:GlobalMaxFailedLoginAttempts` | `20` | Attempts from all addresses before a global lockout |
| `NetworkAccess:LockoutMinutes` | `15` | Lockout duration and how long failures are remembered |
| `NetworkAccess:SessionLifetimeHours` | `12` | Absolute lifetime of a password session |

### Limitations

- Single user and a single password; no accounts, roles or two-factor authentication.
- Terminal sessions belong to the browser session that opened them. Signing out or session expiry makes them unreachable but does not close them; they are closed on application exit.
- Attempt counters and sessions live in memory: a restart resets lockouts and signs everybody out.
- At most 1024 password sign-in clients are tracked. When all entries are still active, new clients receive `429` until an entry expires or a successful sign-in releases it. Requests rejected by the global lockout do not allocate client entries.
- A global lockout caused by an attacker also blocks the owner's password sign-in from other devices until it expires; the local launch link still works.
- Behind a reverse proxy the client address is the proxy, so all clients share one per-client counter; forwarded headers are not trusted. The proxy must keep the original `Host` and `Origin`.
- TermForge does not create certificates. Use a certificate trusted by the client devices (for example from a private CA); a self-signed certificate makes browsers warn on every visit.
- When the only binding is a wildcard (`0.0.0.0`, `[::]`) the local browser is opened at `127.0.0.1` or `[::1]` on that port, where an HTTPS certificate may not match. Keep a separate loopback HTTP endpoint for local use.

## Read-only servers and confirmations

- A server marked read-only rejects writes with `423 Locked` (`code: ReadOnlyServer`) in the backend services, before any SSH connection is opened. The flag is not only a UI hint.
- Dangerous operations (remote delete, Docker stop and restart, rollback, overwriting upload) require a confirmation token from `POST /api/confirmations`. The token is bound to the operation, server and target, expires after 2 minutes and is consumed on first use.
- Details: `docs/decisions/read-only-mode-and-confirmations.md`.

## Logging

Never log secrets: bootstrap token, session token, CSRF token, passwords, password hashes, keys, terminal output. Sign-in attempts are logged with the client address only. Request logging must not include request bodies or cookie headers. The ASP.NET Core request log contains the URL without the fragment, which is why the bootstrap token travels in the fragment.

## Terminal recordings

Terminal output is written to disk only when the user turns on "Record terminal output" in Settings and then starts a recording for a specific terminal; both steps warn that recordings may contain secrets. Keystrokes are never recorded. Recordings are plain asciicast files in the app data directory (directory 0700, files 0600 on Unix), limited in size, count and age, and can be deleted from Settings. See `docs/decisions/terminal-recording.md`.
