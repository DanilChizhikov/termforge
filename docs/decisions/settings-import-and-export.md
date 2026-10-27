# Settings import and export

## Decision

Settings move between computers as two files that the backend produces and the browser saves:

- `termforge-settings-YYYY-MM-DD.json` — a versioned JSON document without secrets.
- `termforge-secrets-YYYY-MM-DD.tfvault` — an optional encrypted archive with saved passwords. It is created only when the user turns on "Also export secrets", types an archive password twice and confirms that anyone with the archive and the password can sign in to the servers.

The backend never writes either file to disk: `POST /api/settings-transfer/export` returns the settings document as text and the archive as base64 of its encrypted bytes, and the browser saves them through a download. Plaintext secrets exist only in backend memory while the archive is built or read and are zeroed after use.

## Settings file

```json
{
  "format": "termforge-settings",
  "schemaVersion": 1,
  "exportId": "1f6c…",
  "exportedAt": "2026-10-27T10:00:00Z",
  "hasSecretsArchive": true,
  "connections": [{ "id": "…", "name": "web-01", "groupName": "Production", "host": "203.0.113.24", "port": 22, "userName": "deploy", "authMethod": "Password", "privateKeyPath": null, "jumpHostId": null, "sshAlias": null, "reconnect": { "mode": "Manual", "maxAttempts": 5, "initialDelaySeconds": 2, "maxDelaySeconds": 60 } }],
  "preferences": { "terminalRecording": { "maxRecordingSizeMegabytes": 10, "maxRecordings": 50, "retentionDays": 30 }, "commandExclusionPatterns": ["…"] },
  "workspace": { "selectedConnectionId": "…", "terminalTabs": [], "…": "same shape as GET /api/workspace state" },
  "pinnedCommands": [{ "connectionId": "…", "command": "docker compose ps" }]
}
```

- Sections are optional: a section the user did not select is `null`. The export dialog offers server connections, preferences, workspace layout and open tabs, and pinned commands (off by default).
- Never exported: passwords (only in the archive), private key files (only their paths), unsaved editor content (it may hold secrets and lives with the workspace on this computer), command history other than pinned commands, trusted host keys, terminal recordings, the operation journal, and configuration from `appsettings.json` (network access, update check, paths).
- Preferences are the settings stored by the application: terminal recording limits and command history exclusion patterns. Whether recording is turned on is not exported, and an import never turns recording on: that stays an explicit choice in Settings.

## Secrets archive

The archive reuses the credential vault format and crypto (`Infrastructure/Credentials/Vault`): PBKDF2-HMAC-SHA256 with a random 32-byte salt and the vault iteration count (`Credentials:VaultKdfIterations`, 600 000 by default), HKDF-derived AES-256-GCM key and password verifier, a random 96-bit nonce, and the whole header as associated data. The plaintext is the vault entry list:

- `export-id` — the `exportId` of the settings file the archive belongs to;
- `connection:<id>:password` and `connection:<id>:sudo-password` — the saved SSH and sudo passwords of exported connections, keyed by the connection id in the settings file.

On import a wrong password is detected by the verifier before decryption ("The archive password is incorrect."); any change to the header, ciphertext or tag fails the GCM check ("damaged or modified"); an archive whose `export-id` differs from the settings file, or that names connections the file does not contain, is rejected as belonging to another export. The KDF iteration count read from an untrusted archive is capped at 10 000 000. The archive password must be at least 12 characters.

## Import

Import is two calls with the same request: `POST /api/settings-transfer/import/preview` shows what would change, `POST /api/settings-transfer/import` applies it. The backend keeps nothing between the calls and recomputes the plan from the current data on apply.

1. Read: the settings file is at most 4 MiB, the archive at most 1 MiB. `format` must be `termforge-settings`; `schemaVersion` newer than 1 is rejected as created by a newer TermForge, missing or older values as invalid. The document is then deserialized strictly: unknown properties fail.
2. Validate (the file is untrusted): at most 1000 connections with unique non-empty ids and unique names; every connection passes the saved connection rules plus import rules (no control characters; host, user, SSH alias and key path do not start with `-`; key paths are kept as written because the file may come from another OS); jump hosts point to connections in the file and do not loop; recording limits and exclusion patterns pass the existing validators; the workspace passes the workspace validator; at most 5000 pinned commands of at most 2000 characters without control characters (commands of servers that do not exist after the import are skipped).
3. Plan. Imported connections are matched to saved ones by name (case-insensitive, names are unique):
   - Merge (default): new names are added (keeping the file's id when it is free); an identical connection is unchanged; a connection that differs is a conflict, resolved per connection as **Keep mine** (default), **Use imported** (update in place, the id stays) or **Keep both** (added as `name (imported)`). Saved connections missing from the file stay.
   - Replace: matched connections are updated, new ones added, and saved connections missing from the file are removed together with their saved passwords. Apply refuses to remove anything without `confirmReplace: true`; the UI asks for it with a destructive confirmation.
   - Jump host ids, workspace references and pinned commands are remapped from file ids to the resulting local ids. The final set of connections is checked for unique names and valid jump host chains before anything is written.
4. Apply, in order: connections (jump hosts first, removals last so foreign keys hold), their secrets into the credential store, preferences, workspace, pinned commands.
   - A password is stored before the connection that uses it is saved, so `hasPassword` is true only when the credential store accepted it. If the store is locked or fails, the connection is imported without a password and the result lists a warning.
   - Preferences: recording limits are replaced, recording stays on or off as it is. Exclusion patterns are merged (union) in Merge mode and replaced in Replace mode.
   - Workspace: the layout document is replaced in both modes. Unsaved editor content on this computer is kept and its tabs stay open. References to servers that do not exist after the import are dropped.
   - Pinned commands are always added: a command already in the history of that server is pinned, a new one is added as pinned, a command the exclusion rules reject is skipped.
5. The browser flushes its workspace before applying and reloads after an import that changed the workspace, so the running page does not overwrite the imported layout.

## API

Protected like every `/api/` endpoint (browser session, CSRF, Origin and network access).

- `GET /api/settings-transfer/summary` → `{ connections, groups, pinnedCommands }` for the export dialog.
- `POST /api/settings-transfer/export` `{ sections: { connections, preferences, workspace, pinnedCommands }, secrets: { password, confirmed } | null }` → `{ settings: { fileName, content }, secrets: { fileName, contentBase64 } | null, warnings }`.
- `POST /api/settings-transfer/import/preview` and `POST /api/settings-transfer/import` `{ settingsFile, secretsArchive, password, mode, sections, resolutions, confirmReplace }` → preview `{ exportedAt, sectionsInFile, connections: [{ importedId, name, host, action, resolution, localName, changes, secrets }], removals, preferenceChanges, workspace, pinnedCommands, secrets, warnings }` or result `{ added, updated, unchanged, kept, removed, secretsStored, preferencesImported, workspaceImported, pinnedCommandsAdded, pinnedCommandsSkipped, warnings }`.
- Errors are problem details with `code`: `Invalid`, `UnsupportedVersion`, `TooLarge`, `WrongPassword`, `ArchiveDamaged`, `ArchiveMismatch`, `WeakPassword`, `ConfirmationRequired`, `SecretsUnavailable`.

## Limitations

- Import is not one transaction: everything is validated first, but a database or credential store failure in the middle leaves the part applied so far. Passwords are written before their connections, so a connection never claims a password it does not have.
- Only schema version 1 exists. A later version adds an upgrade step for older files, like the workspace state.
- Settings from `appsettings.json` and read-only flags, host key policy and similar settings that are not stored by the application yet are not part of the file.
- At most 500 pinned commands per server are exported.
- Import and export are not written to the operation journal.
