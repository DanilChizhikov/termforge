# Settings import and export

## Decision

Settings move between computers as one encrypted file, `termforge-settings-YYYY-MM-DD.tfsettings`, that holds the versioned settings document together with the saved passwords of the exported servers. Every export generates a new random key; the export dialog shows it once with a copy button, and the import dialog asks for the file and the key. Anyone with the file and the key can sign in to the servers.

The backend never writes the file or the key to disk and never logs the key: `POST /api/settings-transfer/export` returns the encrypted bytes as base64 together with the key, and the browser saves the file through a download. Plaintext secrets exist only in backend memory while the file is built or read and are zeroed after use.

## Settings document

The document inside the file:

```json
{
  "format": "termforge-settings",
  "schemaVersion": 1,
  "exportId": "1f6c…",
  "exportedAt": "2026-10-27T10:00:00Z",
  "connections": [{ "id": "…", "name": "web-01", "groupName": "Production", "host": "203.0.113.24", "port": 22, "userName": "deploy", "authMethod": "Password", "privateKeyPath": null, "jumpHostId": null, "sshAlias": null, "reconnect": { "mode": "Manual", "maxAttempts": 5, "initialDelaySeconds": 2, "maxDelaySeconds": 60 } }],
  "preferences": { "terminalRecording": { "maxRecordingSizeMegabytes": 10, "maxRecordings": 50, "retentionDays": 30 }, "commandExclusionPatterns": ["…"] },
  "workspace": { "selectedConnectionId": "…", "terminalTabs": [], "…": "same shape as GET /api/workspace state" },
  "pinnedCommands": [{ "connectionId": "…", "command": "docker compose ps" }]
}
```

- Sections are optional: a section the user did not select is `null`. The export dialog offers server connections, preferences, workspace layout and open tabs, and pinned commands (off by default).
- Never exported: private key files (only their paths), unsaved editor content (it may hold secrets and lives with the workspace on this computer), command history other than pinned commands, trusted host keys, terminal recordings, the operation journal, and configuration from `appsettings.json` (network access, update check, paths).
- Preferences are the settings stored by the application: terminal recording limits and command history exclusion patterns. Whether recording is turned on is not exported, and an import never turns recording on: that stays an explicit choice in Settings.

## Encrypted file and key

The key is 32 random symbols of the Crockford Base32 alphabet (160 bits), shown in eight groups of four: `7K3M-9QX2-…`. On import spaces, dashes and case are ignored, and `O`, `I`, `L` are read as `0`, `1`, `1`.

The file reuses the credential vault format and crypto (`Infrastructure/Credentials/Vault`) with the key as the password: PBKDF2-HMAC-SHA256 with a random 32-byte salt and the vault iteration count (`Credentials:VaultKdfIterations`, 600 000 by default), HKDF-derived AES-256-GCM key and password verifier, a random 96-bit nonce, and the whole header as associated data. The plaintext is the vault entry list:

- `export-id` — the `exportId` of the settings document;
- `settings` — the settings document as UTF-8 JSON;
- `connection:<id>:password` and `connection:<id>:sudo-password` — the saved SSH and sudo passwords of exported connections, keyed by the connection id in the document.

On import a wrong key is detected by the verifier before decryption ("The key is incorrect."); any change to the header, ciphertext or tag, a plain JSON file or any other file fails as damaged; secrets whose `export-id` differs from the document or that name connections the document does not contain are rejected as damaged. The KDF iteration count read from an untrusted file is capped at 10 000 000. The older two-file format (plain JSON plus `.tfvault` archive) is not supported.

## Import

Import is two calls with the same request: `POST /api/settings-transfer/import/preview` shows what would change, `POST /api/settings-transfer/import` applies it. The backend keeps nothing between the calls and recomputes the plan from the current data on apply.

1. Read: the encrypted file is at most 16 MiB and the settings document inside it at most 4 MiB. `format` must be `termforge-settings`; `schemaVersion` newer than 1 is rejected as created by a newer TermForge, missing or older values as invalid. The document is then deserialized strictly: unknown properties fail.
2. Validate (the file is untrusted): at most 1000 connections with unique non-empty ids and unique names; every connection passes the saved connection rules plus import rules (no control characters; host, user, SSH alias and key path do not start with `-`; key paths are kept as written because the file may come from another OS); jump hosts point to connections in the file and do not loop; recording limits and exclusion patterns pass the existing validators; the workspace passes the workspace validator; at most 5000 pinned commands of at most 2000 characters without control characters (commands of servers that do not exist after the import are skipped).
3. Plan. Imported connections are matched to saved ones by name (case-insensitive, names are unique):
   - Merge (default): new names are added (keeping the file's id when it is free); an identical connection is unchanged; a connection that differs is a conflict, resolved per connection as **Keep mine** (default), **Use imported** (update in place, the id stays) or **Keep both** (added as `name (imported)`). Saved connections missing from the file stay.
   - Replace: matched connections are updated, new ones added, and saved connections missing from the file are removed together with their saved passwords. Apply refuses to remove anything without `confirmReplace: true`; the UI asks for it with a destructive confirmation.
   - Jump host ids, workspace references and pinned commands are remapped from file ids to the resulting local ids. The final set of connections is checked for unique names and valid jump host chains before anything is written.
4. Apply under the workspace write coordinator: snapshot every password that may change, then use one SQLite transaction for connections (jump hosts first, removals last so foreign keys hold), preferences, workspace and pinned commands. Once apply starts, closing the page or cancelling its HTTP request does not interrupt it.
   - A shared credential coordinator is held from snapshot through apply or compensation. Normal credential reads, writes and deletions wait for it; the import uses the explicit underlying store under that lease. A concurrent password save therefore runs after compensation rather than being overwritten by an older snapshot. There is no ambient transaction mode or nested credential lock.
   - Password and sudo-password snapshots remain only in zeroed-on-dispose `Secret` buffers. NotFound is distinct from an unavailable credential store; snapshots must all succeed before any mutation. Secrets are applied before the database commit. The first failure stops the import, rolls back the database and restores attempted credential changes in reverse order, including a failed native operation whose outcome is unknown. Password failures are import errors rather than a successful partial result with warnings.
   - Preferences: recording limits are replaced, recording stays on or off as it is. Exclusion patterns are merged (union) in Merge mode and replaced in Replace mode.
   - Workspace: the layout document is replaced in both modes. Reading local drafts and writing the replacement happen under the same coordinator, and the store joins the import transaction. Unsaved editor content on this computer is kept and its tabs stay open. References to servers that do not exist after the import are dropped.
   - Pinned commands are added in bulk with one SaveChanges: a command already in the history of that server is pinned, a new one is added as pinned, a command the exclusion rules reject is skipped. Repeated commands do not create duplicate entries, including entries outside the normal history page limit.
   - Terminal recording retention and other runtime effects run after commit. If recording cleanup fails after commit, the response says the settings were imported and reports the cleanup warning; it does not claim the committed import was rolled back.
5. The browser flushes its workspace before applying and reloads after an import that changed the workspace, so the running page does not overwrite the imported layout.

## API

Protected like every `/api/` endpoint (browser session, CSRF, Origin and network access).

- `GET /api/settings-transfer/summary` → `{ connections, groups, pinnedCommands }` for the export dialog.
- `POST /api/settings-transfer/export` `{ sections: { connections, preferences, workspace, pinnedCommands } }` → `{ file: { fileName, contentBase64 }, key, secretCount }`.
- `POST /api/settings-transfer/import/preview` and `POST /api/settings-transfer/import` `{ file, key, mode, sections, resolutions, confirmReplace }` → preview `{ exportedAt, sectionsInFile, connections: [{ importedId, name, host, action, resolution, localName, changes, secrets }], removals, preferenceChanges, workspace, pinnedCommands, secrets, warnings }` or result `{ added, updated, unchanged, kept, removed, secretsStored, preferencesImported, workspaceImported, pinnedCommandsAdded, pinnedCommandsSkipped, warnings }`.
- Errors are problem details with `code`: `Invalid`, `UnsupportedVersion`, `TooLarge`, `WrongKey`, `FileDamaged`, `ConfirmationRequired`, `SecretsUnavailable`, `ImportFailed`, `CompensationFailed`. The last two return HTTP 500; `CompensationFailed` explicitly requires restoring affected passwords before reconnecting or retrying.

## Limitations

- Credential compensation protects ordinary failures, not process crashes or power loss: snapshots are in memory, and there is no durable import recovery log. A failure of compensation itself is an explicit error and may require restoring saved passwords manually. A timed-out native write may have completed before its helper was stopped, so compensation also covers that attempted mutation.
- The key cannot be recovered: a file whose key is lost cannot be imported.
- Only schema version 1 exists. A later version adds an upgrade step for older files, like the workspace state.
- Settings from `appsettings.json` and read-only flags, host key policy and similar settings that are not stored by the application yet are not part of the file.
- At most 500 pinned commands per server are exported.
- Import and export are not written to the operation journal.
