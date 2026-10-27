# Release verification

The release archives of 1.0.0 were built on a developer machine (macOS 26, Apple silicon, Docker Desktop) on 2026-10-27, because the GitHub Actions minutes had run out when the work started. The macOS and Linux archives were started there. Windows was checked by the `ci` workflow on GitHub runners after the minutes came back (run 40486481500, all jobs green on Ubuntu, macOS and Windows Server 2025).

## Builds

`scripts/publish.sh` builds the web UI once and publishes a self-contained TermForge for six runtime identifiers. Each archive contains the .NET runtime, `appsettings.json` and the UI in `wwwroot`.

`scripts/smoke-test.sh` then starts a published binary from outside its folder, with a temporary database and a fake browser command that captures the one-time launch link. It exchanges the link for a browser session, reads `GET /api/session`, checks that `GET /` returns the bundled UI, prints `GET /api/credentials/status`, asks for exit through `POST /api/lifecycle/exit` and expects the process to end with exit code 0.

| Archive | Where it ran | Smoke test | Credential store seen by the smoke test |
|---|---|---|---|
| `osx-arm64` | macOS 26, Apple silicon | passed | macOS Keychain |
| `osx-x64` | same machine under Rosetta 2 | passed | macOS Keychain |
| `linux-arm64` | Docker, `ubuntu:24.04` and `debian:bookworm-slim` | passed | encrypted vault, because `libsecret-1-0` is not installed |
| `linux-arm64` | Docker, `ubuntu:24.04` with `libsecret-1-0`, `gnome-keyring` and a D-Bus session | passed | Secret Service (libsecret) |
| `linux-x64` | Docker, `ubuntu:24.04` with `--platform linux/amd64` | passed | encrypted vault |
| `win-x64` | CI, `windows-latest` (Windows Server 2025), published on the runner by the same script | passed | Windows Credential Manager |
| `osx-arm64`, `linux-x64` | CI, `macos-latest` and `ubuntu-latest`, published on the runner | passed | macOS Keychain, encrypted vault |
| `win-arm64` | not started | not run | not checked |

The release `win-x64` archive was cross-built on macOS from the same sources and the same runtime pack as the one smoke-tested in CI; its contents were checked (`TermForge.exe`, `wwwroot/index.html`). No arm64 Windows machine was available, so `win-arm64` is built but not verified.

Three problems found by the smoke test were fixed before the release:

- A published TermForge took its content root from the current directory. Started from any other folder, it served no UI and ignored `appsettings.json` (and with it the default port and log levels). It now uses its own folder when that folder contains the bundled UI (`PublishedContentRoot`, covered by `PublishedContentRootTests`).
- The release workflow published without building the UI. It now calls `scripts/publish.sh`, which fails when `wwwroot/index.html` is missing.
- On Windows the fake browser command lost the launch token, because `cmd` splits arguments at `=`. The helper now writes the whole command line.

Linux needs ICU: in a container without `libicu` the runtime stops at start. [installation.md](installation.md#linux) lists the packages.

## Credential store per platform

| Platform | Automated | Manual |
|---|---|---|
| macOS | `OperatingSystemCredentialStoreTests` round-trip against the real login keychain on the test machine | smoke test reports `OperatingSystemStore`, macOS Keychain |
| Linux | the same tests report inconclusive with the reason when no Secret Service is reachable, as on CI runners; `EncryptedVaultTests` and `CredentialBackendSelectorTests` cover the vault and the fallback | smoke test with gnome-keyring reports Secret Service; without libsecret it reports the vault and the install hint |
| Windows | `OperatingSystemCredentialStoreTests.RoundTrip_OnCurrentPlatform_StoresReadsReplacesAndDeletesSecret` passed against Windows Credential Manager on `windows-latest` | smoke test in CI reports `OperatingSystemStore`, Windows Credential Manager |

## MVP acceptance criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | Launch the application on Windows, macOS and Linux | met on x64 and arm64 for macOS and Linux and on x64 for Windows; Windows arm64 not verified | smoke tests above; `ApplicationExitTests`, `ApplicationShutdownTests`, `GracefulShutdownTests`, `LoopbackBindingStartupTests`, `HostStartupTests` |
| 2 | Connect to saved SSH servers with host key verification | met | `HostKeyTrustFlowTests` (unknown key accepted once or saved, rejected, changed key blocked and replaced only after confirmation, revoked key, jump host prompts per hop), `HostKeyVerifierTests`, `KnownHostsLineParserTests`, `SavedConnectionServiceTests`, `SavedConnectionEndpointsTests`, `HostKeyDialog.test.tsx`, `ServerForm.test.tsx` |
| 3 | Use multiple independent interactive terminal sessions | met | `TerminalSessionManagerTests.ConcurrentSessions_HaveSeparatePtysAndShellState`, `OpenedSession_ExchangesInputAndOutputAndResizeReachesRemotePty`, `TerminalSessionTests`, `TerminalEndpointsTests`, `TerminalWorkspace.test.tsx` |
| 4 | Upload, download and edit remote files with conflict protection | met | `TransferQueueTests` (integrity by hash, cancel, interrupted then resumed, overwrite decision), `RemoteFileEditorTests.Save_AfterExternalModification_ReturnsConflictAndKeepsServerContent`, `RemoteFileSaverTests`, `SudoFileEditorTests`, `EditorEndpointsTests`, `EditorWorkspace.test.tsx`, `TransferPanel.test.tsx` |
| 5 | Perform basic Docker operations and open a shell inside a running container | met by unit, endpoint and UI tests | `ContainerActionServiceTests`, `ContainerShellLauncherTests`, `ContainerLogServiceTests`, `DockerContainerListParserTests`, `DockerEndpointsTests`, `DockerCommandIntegrationTests`, `DockerWorkspace.test.tsx`. The integration tests reach a real SSH server but no Docker daemon behind it, so start, stop and container shell run against a substituted command runner |
| 6 | Restore workspace state after restarting the application | met | `WorkspaceStateStoreTests`, `WorkspaceStateLoaderTests`, `WorkspaceStateUpgraderTests`, `WorkspaceEndpointsTests`, `WorkspaceRestore.test.tsx`, `TerminalWorkspace.restore.test.tsx`, `EditorWorkspace.restore.test.tsx` |
| 7 | Store credentials securely and export them only in an encrypted archive | met, with the vault limitation below | `OperatingSystemCredentialStoreTests`, `EncryptedVaultTests`, `SecretTests`, `SecretLoggingTests`, `SecretsArchiveCipherTests`, `SettingsTransferEndpointsTests.Export_NeverWritesPlaintextSecretsToTheFilesTheDatabaseOrTheLogs`, `ExportSecrets_RequiresConfirmationAStrongPasswordAndConnections`, `ExportSettingsDialog.test.tsx`; smoke tests for the store per platform |
| 8 | Receive actionable diagnostics for unavailable features | met | `CapabilityDiagnosticsServiceTests`, `CapabilityProbeTests`, `DiagnosticsEndpointsTests`, `DockerFailureClassifierTests`, `DockerCommandIntegrationTests.ContainerList_OnServerWithoutDocker_ReportsDockerMissing`, `ServerDiagnostics.test.tsx`, `CredentialBackendSelectorTests` (store diagnostics) |
| 9 | Run automated tests covering the critical workflows | met | see [Test run](#test-run) |

## Test run

Results of the verification batch on the release commit:

| Suite | Result |
|---|---|
| `dotnet test termforge.slnx` | 1597 passed, 0 failed, 0 skipped: Core 865, Entrypoint 433, Ssh.IntegrationTests 123, HostKeys 70, Credentials 55, Persistence 51 |
| `npm test` in `src/Web` | 357 passed in 57 files |
| `npm run lint`, `npm run typecheck`, `npm run build` | passed |

The same suites passed in CI on `ubuntu-latest`, `macos-latest` and `windows-latest`. One existing test, `SettingsTransferEndpointsTests.Export_NeverWritesPlaintextSecretsToTheFilesTheDatabaseOrTheLogs`, failed once on `macos-latest` with a file lock on `termforge.db` while it scanned the database folder, and passed on the rerun.

The SSH integration tests ran against OpenSSH servers in Docker. In CI they run only on Linux, because macOS and Windows runners cannot start Linux containers.

## Known limitations of 1.0.0

- The Windows arm64 build is not verified.
- The encrypted vault has no unlock screen. Users without a working system credential store unlock it from the browser developer console, see [credentials.md](credentials.md).
- macOS builds are not notarized. A downloaded copy needs the quarantine attribute removed, see [installation.md](installation.md#macos).
- There is no `linux-musl` build.
- Deferred by the specification: two-way directory sync, full Docker administration, remote access by default, free panel layout, plugins and automatic updates.
