# Command history and reconnect

## Status

Accepted.

## Context

PROJECT_SPEC sections 5 and 6 ask for configurable reconnect, a fresh shell after reconnect, and per-server command history with timestamp and exit code where available, without storing terminal output.

## Decision

- Reconnect is server-side orchestration over the existing `TerminalLauncher`: `ReconnectService` reads the policy of the saved connection, `ReconnectScheduler` runs attempts with exponential backoff using an injected `TimeProvider`, and only transient connection errors are retried. The browser asks `POST /api/terminals/reconnect`; there is no shared connection pool, so a reconnect is an ordinary new SSH connection and shell. Nothing from the old session is buffered, so replay is impossible by construction.
- The policy lives on `saved_connections` (mode, attempts, first and longest wait). `ValueGeneratedNever` is required on the mode column: `Off` is the CLR default of the enum and EF would otherwise substitute the database default.
- History detection uses shell integration sequences (OSC 633;E for the command, OSC 133/633;D for the exit code). The parser (`ShellIntegrationParser`) is a byte-level state machine that observes the output stream after it was forwarded to the browser; it never modifies the stream and only the extracted command line leaves it. Alternatives rejected: reconstructing the command line from keystrokes (wrong with history expansion, tab completion, paste, editors), and parsing the prompt (shell specific, fragile).
- Capture is opt-in. The snippet is inserted into the terminal without a newline so the user reviews and runs it; TermForge never types into a shell on its own and does not modify remote startup files.
- Secrets: leading whitespace and regular expression exclusion applied before persistence (`CommandExclusionRules`), patterns stored in `app_settings`.
- Retention: 1000 unpinned entries per server, pinned entries kept.

## Limits

See "Detection and its limits" in `docs/reconnect-and-command-history.md`: no detection without integration, bash/zsh only, multi-line commands not insertable, exit code unavailable for commands still running when the session ends, and secret detection is best effort.
