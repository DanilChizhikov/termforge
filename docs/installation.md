# Installation

Published packages contain one self-contained executable, the `wwwroot` interface and configuration files. Keep these together. No separate .NET installation is required. Native libraries are bundled and extracted by .NET at startup, so the runtime extraction directory must be writable and permit loading libraries. `DOTNET_BUNDLE_EXTRACT_BASE_DIR` can select another extraction directory. User data remains in the configured data directory, outside the executable; replacing a package does not migrate or delete that data.

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

TermForge starts a local server on `http://127.0.0.1:5023` and opens a private launch file in your default browser. The file redirects to a one-time link that expires after 60 seconds; its token is never passed to the browser in process arguments. Keep the terminal window open: the server runs there and prints its log. Closing the browser tab does not stop it.

Stop TermForge with Exit app in the header, or press `Ctrl+C` in its terminal. If work is still running, the exit dialog lists active transfers, unsaved files and open terminals first.

The launch link works once. Reloading the authenticated tab preserves access. A new tab without its origin proof needs password sign-in in network mode or a restart of TermForge. If the browser lost the session, stop TermForge and start it again.

## Where data lives

| Data | Windows | macOS | Linux |
|---|---|---|---|
| Database (servers, workspace, journal, history) | `%LOCALAPPDATA%\termforge\termforge.db` | `~/Library/Application Support/termforge/termforge.db` | `$XDG_DATA_HOME/termforge/termforge.db`, by default `~/.local/share/termforge/termforge.db` |
| Encrypted vault, if used | `credentials.vault` in the same folder | same | same |
| Terminal recordings, if turned on | `recordings` in the same folder | same | same |
| Passwords in the system store | Credential Manager, `termforge:<key>` | login keychain, service `termforge` | Secret Service, schema `com.dtech.termforge.Credential` |

Private keys stay where they are. TermForge stores only their paths.

## Update

TermForge checks GitHub releases at start and shows a notice when a newer version exists. In Settings → Updates, choose **Install update** to prepare the latest stable package for your current platform. The download is checked against the release manifest before any installed files change. Only published packages support in-app installation; SDK and development launches explain why installation is unavailable.

Before restart, review active transfers, terminals and unsaved files. Choose to wait for transfers, cancel them and restart, or cancel the update. Waiting pauses new work and can be cancelled. Keep the confirmation dialog open while waiting; the browser saves the latest drafts again before requesting restart. A failed draft save blocks restart. Terminal processes close; their tabs and editor drafts return through workspace restoration.

Writable published installations allow one application process per installation directory. The application and updater coordinate with file locks so a second process cannot write data while a snapshot or restoration is running.

After shutdown, TermForge backs up the database, encrypted vault, recordings and application configuration using their configured paths. Private keys and passwords in the system credential store stay in place. Custom `appsettings*.json`, launch arguments and working directory are preserved. The installation directory and its parent must be writable, with enough space for staging and backups. Symbolic links and junctions in managed paths are rejected.

One previous application version and its data snapshot are retained until the next successful update. **Roll back** warns that changes since that snapshot will be lost and offers to keep a separate copy of current data (enabled by default). Backups live in a private `.termforge-updates-<installation-id>` directory beside the installation. Explicitly retained current-data copies are not automatically pruned.

If startup or migration fails, the previous application opens a restricted recovery page without running migrations or normal services. Data stays as it is until you choose **Restore and restart**. Recovery can also keep a copy of current data. Use the new private browser launch link to authenticate after each restart. Do not remove the backup directory while an update or recovery is pending. If an interrupted file replacement prevents the executable from starting, run the retained `helper/TermForge` (`TermForge.exe` on Windows) with `--update-worker <installation-directory> <operation-id>` using the pending operation directory name as its id; keep the original backup directory in place.

Manual installation remains available: exit TermForge, unpack the new archive and start the new binary. Your external data stays in place; copy your configuration changes into the new folder. See [update-artifacts.md](update-artifacts.md) for integrity verification and its trust boundary.

## Uninstall

Delete the unpacked folder. To remove your data as well, delete the `termforge` data folder from the table above and the `termforge` entries in your system credential store.
