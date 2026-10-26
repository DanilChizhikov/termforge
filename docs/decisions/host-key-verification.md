# Host key verification

## Decision

Every SSH hop (terminal, SFTP, jump hosts) goes through `SshClientConnector`, which calls one `IHostKeyVerifier` (`DTech.HostKeys.HostKeyVerifier`). Unknown and changed keys are never accepted silently.

### Sources of trust

- The user's OpenSSH `known_hosts` files, read-only. Default `~/.ssh/known_hosts`; override with `HostKeys:KnownHostsFiles` (list, `~` expanded). Supported: plain and hashed (`|1|salt|hash`) host fields, `[host]:port`, comma separated patterns, `*` and `?` wildcards, `!` negation, `@revoked`.
- The application's own trusted keys in SQLite, table `trusted_host_keys` (host, port, algorithm, `SHA256:` fingerprint, UTC creation time). Hosts are stored lower case.

**The application never writes the system `known_hosts`.** It is shared with OpenSSH and other tools, may be hashed or managed by configuration management, and a bug or a "replace" in TermForge must not weaken trust for other programs. Accepting and saving a key or replacing a changed key only writes to `trusted_host_keys`.

### Statuses

| Status | Rule | Result |
|---|---|---|
| Revoked | a matching `@revoked` entry has the presented key | rejected immediately, no prompt |
| Trusted | the presented key is in `known_hosts` or `trusted_host_keys` | connects |
| Changed | a known key of the same algorithm exists, none matches | blocked, prompt shows a warning |
| Unknown | no known key of that algorithm | paused, prompt |

Keys of other algorithms do not make a key "changed" (OpenSSH behaves the same way); they are shown as known keys in the prompt.

`@cert-authority` lines are ignored: SSH.NET does not expose host certificates, so a host trusted only through a CA is treated as unknown and the user is prompted.

### Pending decisions

SSH.NET raises `HostKeyReceived` synchronously during key exchange, and the server closes connections that do not finish key exchange in time. So the flow is two-phase (as anticipated in `0001-ssh-transport.md`):

1. The gate rejects a non-trusted key during key exchange, before any credential is sent.
2. The connector asks `IHostKeyTrustPrompt`. `HostKeyDecisionBroker` registers a pending decision and awaits it.
3. On accept the hop reconnects with the approved key pinned. If the server presents another key on the reconnect, the connection is rejected.

Pending decisions time out after `HostKeys:DecisionTimeout` (default 2 minutes) and the connection fails closed with `HostKeyRejected` (or `HostKeyChanged`). Without the broker registered, `AddSshTransport` uses a prompt that always rejects.

### Decisions

`POST /api/host-keys/pending/{id}/decision` with `{ "decision": "acceptOnce" | "acceptAndSave" | "reject" | "replace", "confirmReplace": bool }`.

- Unknown key: `acceptOnce` (this connection only), `acceptAndSave` (stored in SQLite), `reject`. `replace` returns 409 `NotAllowed`.
- Changed key: only `reject` or `replace`. `acceptOnce`/`acceptAndSave` return 409 `NotAllowed`; `replace` without `confirmReplace: true` returns 428 `ConfirmationRequired`. `replace` removes the app's saved keys of that algorithm for the endpoint and stores the new one. Stale `known_hosts` entries stay untouched; the saved key takes priority because a matching key means trusted.
- Unknown or expired id: 404 `NotFound`.

`GET /api/host-keys/pending` lists waiting decisions. Changes are pushed on the shared WebSocket `/api/ws` as `hostKeys.pendingChanged` with the full pending list. The UI shows a modal dialog with endpoint, algorithm and `SHA256:` fingerprint; for a changed key it shows a warning and the replace button is enabled only after a separate confirmation checkbox.

## Known limitations

- Host certificates and `@cert-authority` are not supported (prompted as unknown).
- `known_hosts` is re-read on every verification; very large files cost some time per connection.
- Saved keys cannot be listed or removed from the UI yet.
- The saved connection test has a 15 second overall timeout that includes the host key decision; a slower decision cancels the prompt and the test reports a timeout. Decide sooner and run the test again.
- Only the first pending decision is shown at a time; the rest wait in order.
- The test sshd disables `PerSourcePenalties`: OpenSSH 9.8+ penalises clients that close during key exchange, which the two-phase flow does on purpose. A real server with strict penalties may delay the reconnect after a decision.
