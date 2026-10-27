# Troubleshooting

## TermForge does not start

| Message in the terminal | Cause and fix |
|---|---|
| `Failed to bind to address http://127.0.0.1:5023: address already in use` | Another TermForge, or another program, uses the port. Exit the other TermForge, or start this one on another port: `./TermForge --Kestrel:Endpoints:Http:Url=http://127.0.0.1:5199` |
| `Couldn't find a valid ICU package installed on the system` (Linux) | Install ICU, see [installation.md](installation.md#linux) |
| A binding error that mentions loopback or network access | The configuration listens on a non-loopback address. TermForge refuses that unless network access mode is set up, see "Network access mode" in [security.md](security.md#network-access-mode) |
| macOS says the developer cannot be verified | Remove the quarantine attribute, see [installation.md](installation.md#macos) |

## The browser did not open

TermForge opens the default browser with `open` on macOS, `xdg-open` on Linux and the shell on Windows. On a machine without a desktop session nothing opens, and TermForge never prints the launch link, because the link grants full access and must not end up in logs.

To use another browser, point `Lifecycle:BrowserCommand` at its executable. TermForge passes the link as the only argument:

```sh
./TermForge --Lifecycle:BrowserCommand=/usr/bin/firefox
```

## The page says the session is missing or the launch link was already used

The launch link works once, and the session lives only as long as the browser keeps its session cookie. If you closed every browser window or opened the link in a second browser, stop TermForge (`Ctrl+C`) and start it again.

## Passwords are not saved, or TermForge asks for a master password

TermForge uses the system credential store when it passes a self-test on first use, and an encrypted vault file otherwise. The UI does not show which one is active yet. Ask the API from the developer console of the TermForge tab: `await (await fetch('/api/credentials/status')).json()` returns the backend and the reason. On Linux the usual cause is a missing `libsecret-1-0` or a locked keyring. The fixes for each diagnostic are in [credentials.md](credentials.md#troubleshooting). Restart TermForge after fixing the store.

The vault has no unlock screen yet. Unlock it from the developer console of the TermForge tab, as shown in [credentials.md](credentials.md#how-secrets-are-stored).

## Connection to a server fails

| Symptom | What to check |
|---|---|
| A host key dialog appears | The server key is not in `~/.ssh/known_hosts` and not trusted in TermForge yet. Compare the `SHA256:` fingerprint with `ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub` on the server before you accept it |
| The connection is blocked because the host key changed | The server key differs from the trusted one. That happens after a reinstall, and also during a man-in-the-middle attack. Replace the key only after you confirmed the new fingerprint on the server. See [decisions/host-key-verification.md](decisions/host-key-verification.md) |
| Authentication fails | Check the user, the key path and the passphrase in the server dialog. Keyboard-interactive and two-factor logins are not supported, see [ssh-capabilities.md](ssh-capabilities.md) |
| A `ProxyJump` chain fails | Only one jump host level is tested. Check that the jump host is saved as its own server |

## A feature is missing on a server

Open the Diagnostics tab for the server. It checks command execution, SFTP, Docker, sudo and common utilities, and says what is missing and why. See [diagnostics.md](diagnostics.md).

| Diagnostic | Fix |
|---|---|
| Docker is not installed | Install Docker on the server |
| The user is not in the docker group | `sudo usermod -aG docker <user>` on the server, then log in again |
| sudo needs a password | Editing with sudo still works and asks for the password. Only the check runs without one |

## A transfer stopped

Failed transfers keep their `.part` file. Retry in the transfer panel or in the History tab continues from it. Discard partial removes it. See [decisions/transfer-queue.md](decisions/transfer-queue.md).

## A file was changed on the server while you edited it

The editor refuses to save over the newer version and offers Compare, Reload from server, or Overwrite after a second confirmation. Each save keeps a backup that you can compare with and roll back to. See [decisions/remote-file-editor.md](decisions/remote-file-editor.md).

## Logs

TermForge writes its log to the terminal it runs in. Secrets, launch tokens and cookies never appear in it. Raise the detail with `--Logging:LogLevel:Default=Debug`.
