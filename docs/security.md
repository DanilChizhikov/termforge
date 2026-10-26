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
- The local network is untrusted. Network access mode is not implemented; the backend refuses to start with a non-loopback binding.

### Threats and mitigations

| Threat | Attack | Mitigation |
|---|---|---|
| Remote access from the network | Connecting to the backend from another machine | Binding validation: every effective listening address must be loopback. `0.0.0.0`, `*`, `+`, `[::]`, LAN addresses, host names other than `localhost`, Unix sockets and `http_ports`/`https_ports` (which bind to all interfaces) are rejected before Kestrel starts listening. Validation follows Kestrel precedence: `Kestrel:Endpoints` are always checked; `urls` are checked when no endpoint is configured or `preferHostingUrls` is set; `http_ports`/`https_ports` only when `urls` is empty as well. Addresses Kestrel ignores (e.g. `ASPNETCORE_HTTP_PORTS=8080` preset in .NET container images) do not block startup |
| DNS rebinding | A malicious site points its own domain at `127.0.0.1` and reads responses as same-origin | Host header allow-list: only `localhost` and loopback IP literals (`127.0.0.0/8`, `::1`) are accepted. A rebinding request carries the attacker's domain in `Host` and gets `400` |
| Cross-site request forgery | A malicious site submits a form or `fetch` to `http://127.0.0.1:5023/api/...` | Session cookie is `SameSite=Strict`; state-changing requests require the `X-CSRF-Token` header that only same-origin script can obtain; `Origin` must match exactly |
| Cross-site WebSocket hijacking | A malicious site opens `ws://127.0.0.1:5023/...`; browsers do not apply CORS to WebSockets | Every WebSocket handshake must carry an `Origin` equal to the backend origin and a valid session cookie |
| Other local web apps | A page served by another local server (e.g. `http://localhost:8080`) is same-site with TermForge, so `SameSite` cookies are sent and non-HttpOnly cookies are readable | `Origin` comparison includes scheme and port; `Sec-Fetch-Site: same-site` is rejected; the session cookie is HttpOnly; the CSRF token is never stored in a cookie |
| Clickjacking | Framing TermForge in a hostile page | `X-Frame-Options: DENY`, `frame-ancestors 'none'` on the bootstrap page; the Strict cookie is not sent in a cross-site frame |
| Other local processes | A process sends HTTP requests to the port without a browser | Every `/api` request and every WebSocket needs the session cookie, which exists only in the browser opened by TermForge. The bootstrap token is single use, 256 bits, and never logged |
| Token leakage | Bootstrap token in server logs, proxy logs, `Referer`, or browser history | The token travels in the URL fragment (never sent to the server), the bootstrap page clears it with `history.replaceState`, sends `Referrer-Policy: no-referrer`, and the token is exchanged with a `POST` body. Tests run with `Trace` logging and assert that no token or cookie value appears in any log message |
| Timing attacks | Guessing secrets byte by byte | All secret comparisons use `CryptographicOperations.FixedTimeEquals` |

### Known limitations

- A process running as the same OS user can read the browser profile, attach a debugger or replace the binary. Protecting against that is out of scope for a local application.
- The session cookie is a browser session cookie for the current launch. If the browser discards it (all windows closed without session restore), the running backend cannot be reached from the browser; stop it with `Ctrl+C` and start it again. Re-opening a session from a second launch is not implemented.
- When opening the browser fails (no default browser, headless machine) or `Lifecycle:OpenBrowser` is `false`, the bootstrap URL is not shown anywhere, by design, because it must not appear in logs.
- Plain HTTP on loopback: the cookie is marked `Secure` only when the request is HTTPS.
- Network access mode with authentication is a separate task ("Network Access Opt-In").

## Browser contract

This is the contract the frontend (`src/Web`) must follow.

### Launch and bootstrap

1. On start the backend generates a random 256-bit bootstrap token (base64url, 43 characters).
2. After Kestrel starts, the backend opens the default browser at:

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

   - `204 No Content` with `Set-Cookie: termforge_session=<session-token>; path=/; samesite=strict; httponly` on the first valid exchange.
   - `403 Forbidden` for an invalid token or any later attempt: the token works exactly once per launch.
4. The script then navigates to `/`. If the page is opened without a token but the browser already has a valid session, it navigates to `/` too.

The SPA does not need to implement any of the bootstrap steps.

### Session cookie

| Property | Value |
|---|---|
| Name | `termforge_session` |
| Flags | `HttpOnly`, `SameSite=Strict`, `Path=/`, `Secure` only over HTTPS, no `Domain`, no expiry (browser session) |
| Lifetime | Current backend launch; a restart invalidates it |

The SPA cannot and must not read it. `fetch` sends it automatically for same-origin requests (`credentials: 'same-origin'`, the default).

### CSRF token

- Obtain it once after the SPA loads: `GET /api/session` returns `200` with `{"csrfToken":"<token>"}` (`Cache-Control: no-store`), or `401` when there is no valid session.
- Keep it in memory only (not in `localStorage`, not in a cookie).
- Send it as the `X-CSRF-Token` header on every `POST`, `PUT`, `PATCH` and `DELETE` request. Without it, or with a wrong value, the backend answers `403`.
- It stays valid for the whole session.

### Requests

- All API endpoints live under `/api`. Every `/api` request except `POST /api/session/bootstrap` requires the session cookie; without it the backend answers `401`. On `401` the UI should tell the user to restart TermForge.
- Use relative URLs (same origin). The backend rejects any request whose `Origin` header differs from its own origin (`403`) and any request the browser marks with `Sec-Fetch-Site` other than `same-origin` or `none` (`403`).
- The `Host` must be `localhost` or a loopback IP (`400` otherwise).
- The SPA shell and static assets outside `/api` are served without a session: they must not contain secrets.

### WebSocket

- Connect to a same-origin URL built from `window.location`, for example `` new WebSocket(`ws://${window.location.host}/api/...`) ``.
- Browsers send `Origin` and the session cookie on the handshake automatically. The backend requires both: missing or foreign `Origin` → `403`, missing or invalid session → `401`. No CSRF header is needed (and browsers cannot set one).
- Terminal sockets `/api/terminals/{id}/ws` additionally require that the terminal id (256 random bits) was created by the same browser session (`404` otherwise) and is not already attached (`409`). A leaked id alone is useless without the session cookie and a same-origin handshake.

### Exit

- `POST /api/lifecycle/exit` with the session cookie and `X-CSRF-Token` returns `202 Accepted`; the backend stops after the response is sent. Closing a browser tab does not stop the backend.

### Development proxy

A dev server (e.g. Vite) works without backend changes:

- Run it on `127.0.0.1` (Vite: `server.host: '127.0.0.1'`), not `localhost`, because cookies are scoped to the host name, not the port.
- Proxy `/api` to `http://127.0.0.1:5023` with WebSocket proxying enabled (`ws: true`) and the original `Host` header kept (`changeOrigin: false`, the Vite default). The backend then sees `Host: 127.0.0.1:5173` and `Origin: http://127.0.0.1:5173`, which match.
- Start the backend; the browser it opens bootstraps the session on `127.0.0.1`. Then open `http://127.0.0.1:5173` in the same browser: the session cookie is sent to the dev server and forwarded by the proxy.

## Request pipeline

`UseLocalHostSecurity()` must be the first middleware call after `builder.Build()` so it guards everything added later (static files, SPA fallback, WebSockets):

1. Host header validation → `400`.
2. Security headers (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Cache-Control: no-store` for `/api`).
3. `UseWebSockets()`. Endpoints must not call it again.
4. Origin and `Sec-Fetch-Site` validation → `403`.
5. Browser session: required for `/api/*` (except the bootstrap exchange) and every WebSocket → `401`.
6. CSRF: required for every `POST`/`PUT`/`PATCH`/`DELETE` (except the bootstrap exchange) → `403`.
7. Bootstrap endpoints.

New endpoints are protected automatically when they are mapped under `/api`. Endpoints that read the session can use `context.Features.Get<BrowserSession>()`.

## Lifecycle

- Startup: binding validation runs in an `IStartupFilter`, before Kestrel binds. A non-loopback binding throws `InsecureBindingException` and the process exits without listening.
- Browser: `BrowserOpeningService` opens the bootstrap URL after the application has started. `Lifecycle:OpenBrowser=false` skips it (tests, headless runs); `Lifecycle:BrowserCommand` sets an executable that receives the URL as its only argument instead of the OS default browser (`open` on macOS, `xdg-open` on Linux, shell execute on Windows). Launch failures are logged without the URL.
- Running: the backend does not depend on browser tabs.
- Exit: the exit endpoint, `Ctrl+C` or `SIGTERM` trigger `IHostApplicationLifetime.StopApplication`. Hosted services and request handlers receive cancellation through their `CancellationToken`/`ApplicationStopping`; the host waits up to `Lifecycle:ShutdownTimeoutSeconds` (default 10) for them, then the process exits.

## Configuration

| Key | Default | Meaning |
|---|---|---|
| `Kestrel:Endpoints:Http:Url` | `http://127.0.0.1:5023` | Listening address; must be loopback |
| `Lifecycle:OpenBrowser` | `true` | Open the browser after start |
| `Lifecycle:BrowserCommand` | not set | Executable used instead of the default browser |
| `Lifecycle:ShutdownTimeoutSeconds` | `10` | Graceful shutdown timeout, must be positive |

## Logging

Never log secrets: bootstrap token, session token, CSRF token, passwords, keys, terminal output. Request logging must not include request bodies or cookie headers. The ASP.NET Core request log contains the URL without the fragment, which is why the bootstrap token travels in the fragment.
