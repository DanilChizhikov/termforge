# Operation journal

## Decision

Long-running and important operations (transfers, saves, Docker actions) are recorded in a local journal stored in the SQLite database (`operations` table).

- Model: id, kind, server, status, UTC timestamps (created, started, finished, updated), sanitized details, error, retryable flag.
- State machine: `queued -> running | cancelled`, `running -> succeeded | failed | cancelled`. Terminal states are final and any other transition throws `OperationException` (`InvalidTransition`).
- Producers use `IOperationJournal` (`Create`, `Start`, `Succeed`, `Fail`, `Cancel`). The journal is scoped; background producers create a DI scope.
- Secrets: `OperationSanitizer` redacts values of keys containing password, passphrase, token, secret, private key, api key or credential, redacts private key blocks and `password=...` style assignments in values and errors, drops keys that look like terminal output (output, stdout, stderr, terminal, transcript, scrollback), and bounds entry count and value length. Terminal output is never stored.
- Retry contract: a feature registers an `IOperationRetryHandler` for its `Kind`. `POST /api/operations/{id}/retry` calls it for failed or cancelled operations created with `Retryable = true`. Without a handler the endpoint returns 501 with code `NoRetryHandler`; a non-retryable operation returns 409.

## API

- `GET /api/operations?kind=&status=&server=&from=&to=&offset=&limit=` newest first, `limit` defaults to 50 and is capped at 200; `from` and `to` are ISO 8601 timestamps filtered on creation time (inclusive).
- `GET /api/operations/{id}`
- `POST /api/operations/{id}/retry`

## Shared realtime channel

There was no WebSocket endpoint, so `/api/ws` is the shared, protected channel for server push. It lives under `/api`, so the Origin and browser session checks apply. Messages are JSON `{ "type": string, "data": object }`. Server code publishes through `RealtimeChannel.Publish(type, payload)`; the journal publishes `operation.changed` with the full operation on every change. The channel is one-way: client messages are ignored. Later tasks add their own message types to the same channel instead of opening new endpoints.

## Known limitations

- Updates are not replayed: a client that reconnects must reload the list.
- Journal rows are never pruned.
- Concrete producers and retry handlers are added by the feature tasks.
