# Installation

TermForge ships as a self-contained archive for each operating system and CPU. The archive includes the .NET runtime and the web UI, so you do not need to install .NET or Node.js.

## Pick an archive

Download the archive for your system from the GitHub releases page of the repository.

| System | Archive | Status |
|---|---|---|
| macOS on Apple silicon | `termforge-<version>-osx-arm64.tar.gz` | verified |
| macOS on Intel | `termforge-<version>-osx-x64.tar.gz` | verified on Apple silicon under Rosetta 2 |
| Linux x64 (glibc) | `termforge-<version>-linux-x64.tar.gz` | verified |
| Linux arm64 (glibc) | `termforge-<version>-linux-arm64.tar.gz` | verified |
| Windows x64 | `termforge-<version>-win-x64.zip` | smoke-tested by the release workflow on `windows-latest` |
| Windows arm64 | `termforge-<version>-win-arm64.zip` | built, not verified |

What "verified" covers is listed in [release-verification.md](release-verification.md). Alpine and other musl-based Linux distributions are not supported: there is no `linux-musl` build.

## macOS

```sh
tar -xzf termforge-1.1.0-osx-arm64.tar.gz
xattr -dr com.apple.quarantine termforge-1.1.0-osx-arm64
./termforge-1.1.0-osx-arm64/TermForge
```

The binaries are ad-hoc signed and not notarized, so Gatekeeper rejects them. A copy downloaded with a browser carries the quarantine attribute, and Finder then refuses to open it with a dialog that the developer cannot be verified. The `xattr` command removes the attribute from the binary and its libraries.

Passwords go to the login keychain. macOS may ask once whether TermForge can use it.

## Linux

```sh
tar -xzf termforge-1.1.0-linux-x64.tar.gz
./termforge-1.1.0-linux-x64/TermForge
```

Required system packages:

- ICU (`libicu`). Without it the process stops at start with a globalization error. Debian and Ubuntu name the package after its version: `libicu72` on Debian 12, `libicu74` on Ubuntu 24.04 (`apt-cache search '^libicu[0-9]'` shows yours). Fedora: `sudo dnf install libicu`.
- OpenSSL and CA certificates, present on almost every desktop install.

Optional:

- `libsecret-1-0` and a running Secret Service provider (GNOME Keyring, KWallet with Secret Service support, or KeePassXC). With them, passwords go to the system keyring. Without them TermForge keeps passwords in an encrypted vault file that you unlock with a master password after every start. See [credentials.md](credentials.md).
- `xdg-open`, which TermForge uses to open the browser.

## Windows

Unpack the `.zip` and run `TermForge.exe`. The x64 build passed the smoke test on Windows Server 2025; the arm64 build has not been started on a real machine yet. The binaries are not signed, so Windows SmartScreen may warn about an unknown publisher. Passwords go to Windows Credential Manager.

## First start

TermForge starts a local server on `http://127.0.0.1:5023` and opens your default browser with a one-time launch link. Keep the terminal window open: the server runs there and prints its log. Closing the browser tab does not stop it.

Stop TermForge with Exit app in the header, or press `Ctrl+C` in its terminal. If work is still running, the exit dialog lists active transfers, unsaved files and open terminals first.

The launch link works once. If the browser lost the session (for example all browser windows were closed), stop TermForge and start it again.

## Where data lives

| Data | Windows | macOS | Linux |
|---|---|---|---|
| Database (servers, workspace, journal, history) | `%LOCALAPPDATA%\termforge\termforge.db` | `~/Library/Application Support/termforge/termforge.db` | `$XDG_DATA_HOME/termforge/termforge.db`, by default `~/.local/share/termforge/termforge.db` |
| Encrypted vault, if used | `credentials.vault` in the same folder | same | same |
| Terminal recordings, if turned on | `recordings` in the same folder | same | same |
| Passwords in the system store | Credential Manager, `termforge:<key>` | login keychain, service `termforge` | Secret Service, schema `com.dtech.termforge.Credential` |

Private keys stay where they are. TermForge stores only their paths.

## Update

TermForge checks GitHub releases at start and shows a notice when a newer version exists. It never updates itself. To update, exit TermForge, unpack the new archive and start the new binary. Your data stays in the folders above. If you changed `appsettings.json`, copy your changes into the new folder.

## Uninstall

Delete the unpacked folder. To remove your data as well, delete the `termforge` data folder from the table above and the `termforge` entries in your system credential store.
