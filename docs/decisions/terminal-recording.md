# Terminal recording

## Decision

Terminal output recording is opt-in twice: the global setting "Record terminal output" is off by default, and even when it is on nothing is recorded until the user starts a recording for one terminal session. Keystrokes (input) are never recorded.

- Tap: `ManagedTerminalSession` (Core/Terminal/Sessions) accepts one `ITerminalOutputObserver`. After every read from the SSH shell it hands the bytes to the observer, if one is attached. Without a recording nothing else happens on the read path. Input written through `WriteAsync` never reaches the observer.
- Off the hot path: `TerminalRecorder.Observe` copies the bytes and does a non-blocking `TryWrite` into a bounded channel (1024 chunks). A single writer task decodes UTF-8 (a decoder keeps characters split across reads), formats events and writes the file. When the channel is full the recording stops and is marked truncated instead of slowing the terminal down.
- Format: asciicast v2. The first line is the header `{"version":2,"width":..,"height":..,"timestamp":..,"title":"<server> #<n>","env":{"TERM":"xterm-256color"}}`, then one `[seconds, "o", "data"]` line per output chunk. Width and height are the terminal size when the recording starts; resizes are not recorded.
- Storage: one `<id>.cast` file and one `<id>.json` metadata file per recording in `<app data>/termforge/recordings` (next to the database; override with `Recordings:Directory`). The directory is created with mode 0700 and files with 0600 on Unix. Metadata (server, session number, size, start and end time, end reason) is written when the recording starts and replaced atomically when it ends, so a crash leaves an "interrupted" recording.
- Ids: 32 random lowercase hex characters (`TerminalRecordingId`). Anything else is rejected with 400 before a path is built, and the resolved path must stay inside the recordings directory.
- Limits: maximum size per recording (1–100 MB, default 10), maximum number of recordings (1–500, default 50) and retention in days (1–365, default 30). A recording that reaches its size limit stops (the terminal keeps running) and is marked truncated. Retention runs at startup, when a recording starts and when settings change; recordings in progress are never deleted.
- Lifecycle: a recording ends when the user stops it, the terminal session closes, the global setting is turned off, the size limit is reached, the writer falls behind or the disk write fails. Settings are stored as JSON in `app_settings` (`terminal_recording.settings`); no migration is needed.

## API

All endpoints are protected like every `/api/` endpoint (browser session, CSRF for unsafe methods, Origin and network access). Terminal endpoints are bound to the browser session that owns the terminal.

- `GET /api/recordings/settings`, `PUT /api/recordings/settings` `{ enabled, maxRecordingSizeMegabytes, maxRecordings, retentionDays }`.
- `GET /api/recordings` list, newest first, with status `recording`, `complete`, `truncated` or `interrupted`.
- `GET /api/recordings/{id}/cast` the asciicast file (`application/x-asciicast`).
- `DELETE /api/recordings/{id}` deletes the file and its metadata (409 while recording).
- `GET|POST|DELETE /api/terminals/{id}/recording` status, start (409 `Disabled` while the global setting is off) and stop.

## UI

- Settings (header button) → Security: the "Record terminal output" switch from the Settings artboard, rows for the size limit, the number of recordings and the retention days, the storage directory, and the recordings table with Replay and Delete (Delete asks for confirmation, Cancel is the default).
- Turning the setting on and starting a recording both show a warning that recordings may contain secrets; Cancel is the default.
- Each connected terminal has a status line (Main artboard "Recording off") with Record / Stop recording and the current size; a recording tab shows a `rec` badge. When recording is off globally the status line links to Settings.
- Replay plays the cast in a read-only xterm.js terminal of the recorded size with Play/Pause and Restart. Pauses longer than 2 s are shortened.

## Tests

- `Core.Tests/Terminal/Recording`: default off, start only per session and only when enabled, input not recorded, file format (header, events, escapes, split UTF-8), size limit truncation, writer overflow, session close, turning the setting off, retention by age and count, active recordings kept, deletion, invalid and traversal ids.
- `Entrypoint.Tests/Recordings`: settings defaults and validation, start rejected while off, record → list → replay → delete over HTTP with a scripted terminal, invalid ids, session required.
- `Persistence.Tests/Settings`: settings round trip.
- Frontend: cast parsing, playback timing, settings warning and limits, recordings list and delete, terminal recording status line.

## Known limitations

- Resizes are not recorded; replay uses the size at the start of the recording.
- A recording belongs to one terminal session. After a reconnect the new session is not recorded until Record is pressed again.
- Recordings are stored unencrypted (with owner-only file permissions) in the app data directory.
- The terminal status line refreshes the size every 3 seconds while recording.
