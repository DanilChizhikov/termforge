# Filename search

## Decision

Each file pane can search names below its current folder. Content search is out of scope.

- Pattern: case-insensitive glob with `*` and `?`; a pattern without wildcards matches as a substring (`log` behaves like `*log*`). Empty patterns, control characters, path separators and patterns longer than 255 characters are rejected.
- Limits: `maxDepth` (default 8, 1..64), `maxResults` (default 500, 1..5000), `timeLimitSeconds` (default 20, 1..300). Values outside the range are rejected with `400 Invalid`.
- Traversal: `FileTreeSearcher` walks the tree breadth-first over `IFileTreeReader` (local disk or SFTP), checks the time limit and the caller's cancellation before every listing and interrupts a hanging listing. Symbolic links are matched by name but never followed, so link loops cannot hang a search.
- Errors: a folder that cannot be read (permission denied, vanished, I/O error) is reported as a `warning` event and the search continues; only an unreadable root fails the search. At most 50 warnings are sent, the summary carries the total.
- Partial results: the final `completed` event carries the outcome (`Completed`, `ResultLimitReached`, `TimeLimitReached`, `Cancelled`, `Failed`), counts and `isPartial`. A search is partial when a limit stopped it, when the depth limit hid deeper folders or when some folders could not be read.
- Remote method: SFTP traversal is the default. `method=find` runs `find` over the exec channel only when command execution is not reported unavailable by the cached capability report and `command -v find` succeeds; otherwise the search falls back to SFTP and the stream starts with a `notice` explaining why.
- Shell safety: the remote `find` command is built from a fixed template. The root path and the pattern are passed as single-quoted arguments (`ShellArgument.Quote`, `'` becomes `'\''`) and the pattern is only ever the argument of `-iname`; `[` and `]` are escaped so only `*` and `?` act as wildcards. The whole script is wrapped once more with `ShellScript.Wrap`. Tests run real `/bin/sh` with injection attempts and check that `find` receives them as one literal argument.
- Remote `find` output: records are NUL-terminated (`printf 'd%s\0'` / `'f%s\0'`) so any byte in a file name, including newlines, is parsed safely. A shell timer kills `find` at the time limit; output is capped relative to `maxResults`. Results are sent after `find` exits (the exec runner is not streaming); SFTP and local searches stream every match as it is found.
- Transport: `GET /api/files/local/search` and `GET /api/files/remote/{connectionId}/search` with `path`, `pattern`, `maxDepth`, `maxResults`, `timeLimitSeconds` and (remote) `method=sftp|find`. The response is NDJSON (`application/x-ndjson`), one event per line, flushed per event. Closing the request (aborting the fetch) cancels the search. Validation errors are returned as problem details before streaming starts.

## UI

The search button in the pane toolbar replaces the file table with the search panel: name pattern, depth, result limit and (remote) method, Search/Stop, live status (searching, complete, partial with reason, failed), fallback notices, unreadable locations and the result list. "Open location" navigates the pane to the parent folder and selects the found entry.

## Known limitations

- Remote `find` results arrive in one batch when `find` finishes; use SFTP traversal for live results.
- Every remote search opens its own SSH connection.
- Hidden files are included; there is no exclude list.
