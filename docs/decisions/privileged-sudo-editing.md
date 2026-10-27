# Privileged sudo editing

## Decision

Files the SSH user cannot read or write are edited through an explicit sudo workflow. The application never runs as root and never runs arbitrary privileged commands: sudo is used only for the fixed file operations of the editor, only after the user chose "Edit with sudo" or "Open with sudo" for a file, and only for the request that carries that action.

The sudo workflow plugs into the existing editor pipeline instead of forking it. `SudoRemoteEditorFileClient` implements the same `IRemoteEditorFileClient` as the SFTP client, so the version check (`ConflictDetector`), backups (`BackupWriter`), replacement (`FileReplacer`), pruning (`BackupPruner`), rollback and journaling (`EditorOperationRecorder`) are the same code for both paths.

## Transport

Each elevated request opens one SSH connection and runs commands over exec channels (`IRemoteShellCommandRunner`), with binary standard output and an optional standard input. SFTP is not used on this path.

## Detection

`GET /api/editor/remote/{id}/sudo/status` runs `LC_ALL=C sudo -n true`:

| Result | State |
|---|---|
| exit 0 | `Passwordless` (NOPASSWD rule or cached credentials) |
| `a password is required` | `PasswordRequired` |
| `sudo` not installed, user not in sudoers | `Unavailable` |

The status also says whether a sudo password for the connection is saved in the credential store. Detection never sends a password.

## Password handling

- The password travels from the browser in the JSON body of the elevated request (never in a URL), is copied into a buffer right away and is written to the remote `sudo -S -p ''` over standard input only. It never appears on a command line, in the process list, in logs, in the operation journal, in error messages or in responses.
- It is kept in memory for the single request only. The buffer holding it is zeroed when the request ends.
- If the user ticks "Remember in credential store", the password is saved under `connection:<id>:sudo-password` after it was verified, and later requests without a password use it. `DELETE /api/editor/remote/{id}/sudo/password` forgets it.
- Before any file operation the password is verified with `LC_ALL=C sudo -S -p '' true` (or `sudo -n true` without a password). Failures are reported as `SudoPasswordRequired`, `SudoPasswordIncorrect` or `SudoUnavailable` and nothing is touched.
- Every elevated command is `LC_ALL=C sudo -S -p '' <command>` with the password line on standard input, or `LC_ALL=C sudo -n <command>` without one. `LC_ALL=C` keeps sudo messages in English so failures can be classified.

## Quoting

Every path and argument goes through `ShellQuoting.Quote` (POSIX single quotes, `'` becomes `'\''`, NUL is rejected). Multi-step privileged steps are fixed `sh -c` scripts that take the quoted values as positional parameters, so no value is ever spliced into script text. Paths are absolute and normalized (no `..`, no control characters) before they reach the command builder, and every command uses `--` before operands.

## Operations

| Client operation | Remote command |
|---|---|
| attributes | `sudo stat -c '%f %s %Y %u %g' -- <path>` (`lstat`: raw mode, size, mtime seconds, uid, gid) |
| read | `sudo head -c <limit + 1> -- <path>` |
| create (backup, temporary sibling) | upload as the SSH user to a `mktemp` file (`0600`, owned by the user) through standard input, then `sudo install -o <uid> -g <gid> -m <mode> -- <staged> <target>` after checking the target does not exist; the staged file is removed in the same command |
| atomic replace | `sudo mv -f -- <temporary> <path>` (rename in the same directory); refuses a directory target |
| in-place overwrite | staged upload, then `sudo sh -c 'cat -- "$1" > "$2"'` (not used by the save pipeline, because root can always keep the owner) |
| change owner | `sudo chown -- <uid>:<gid> <path>` |
| delete | `sudo rm -f -- <path>` |
| list backups | `sudo sh -c` loop over the directory, NUL-separated names |

## Save

The steps are those of the regular editor (see [remote-file-editor.md](remote-file-editor.md)):

1. Read the file with sudo and compare mtime, size and SHA-256 with the expected version. A difference is a `409` conflict and nothing is written. The version format is the same as the SFTP one (mtime in whole seconds), so a file opened without sudo can be saved with sudo.
2. Back up the current content beside the file with the original owner, group and mode.
3. Install the new content into a hidden temporary sibling with the original owner, group and mode (`install -o -g -m` sets the owner before the mode, so set-id bits survive).
4. `mv -f` the temporary file over the original: an atomic `rename(2)` within one directory.
5. Prune backups beyond `Editor:MaxBackupsPerFile`.

A failure before step 4 removes the temporary file and the staged upload and leaves the original untouched. Rollback restores a backup through the same steps.

The owner and group are passed to `install` through `RemoteFileCreation.Owner`. The SFTP client ignores it and keeps its own `chown` step, so the regular editor is unchanged.

## Journal

Saves and rollbacks are journaled as `file.save` and `file.rollback`, like regular saves, with an extra `elevation: sudo` detail. The password is never part of the operation.

## UI

- The Editor toolbar offers "Edit with sudo" for an open file. A file that cannot be opened because of missing permissions offers "Open with sudo".
- When sudo needs a password and none is saved, every elevated action (open, save, compare with a backup, roll back) asks for it in a dialog. Cancel is the default; "Remember in credential store" is off by default.
- After a file is opened, `GET /api/editor/remote/{id}/sudo/access?path=&ownerId=` checks it as the SSH user, without sudo: the owner name comes from `getent passwd <uid>` (falling back to `/etc/passwd`), and the file counts as writable when both `[ -w file ]` and `[ -w directory ]` hold (the save writes a backup and a temporary file beside it). Values are passed as quoted positional arguments of a fixed `sh -c` script. When the file is not writable, the header shows the caution badge "owned by <owner> · save needs sudo" from the Editor artboard; an unknown name falls back to "uid <n>". Nothing is elevated automatically.
- After the user opts in, the same badge slot reads "owned by <owner> · editing with sudo" (an extra state the artboard does not draw), and "Stop using sudo" returns the tab to normal saving. A saved password can be forgotten from the same toolbar.

## Known limitations

- The server needs `sudo`, a POSIX `sh`, `stat -c`, `head -c`, `install`, `mktemp` and `mv` (GNU coreutils or BusyBox).
- The sudo rule must allow these commands; restricted rules that allow only `sudoedit` are not supported.
- `requiretty` in sudoers blocks the workflow, because commands run without a terminal.
- The staged upload lives in the user's temporary directory, so the content briefly exists there with mode `0600` before it is installed.
- The versions list of an elevated file is read without sudo; a directory the SSH user cannot list shows an error there, while saving still works.
- A saved sudo password is not removed automatically when the connection is deleted; forget it before deleting the connection.
