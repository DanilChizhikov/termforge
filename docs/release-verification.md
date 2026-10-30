# Release verification

The release archives of 1.1.0 were built and started on a developer machine (macOS 26, Apple silicon, Docker Desktop) on 2026-10-30 from `develop` at `bcc6e64` plus the documentation changes of this release. Since 1.1.0 the `ci` workflow runs on Linux only; the `release` workflow publishes on Linux, macOS and Windows runners and smoke-tests `linux-x64`, `osx-arm64` and `win-x64` there before it creates the GitHub release.

## Builds

`scripts/publish.sh -v 1.1.0` builds the web UI once and publishes a self-contained TermForge for six runtime identifiers. Each archive contains the .NET runtime, `appsettings.json` and the UI in `wwwroot`.

`scripts/smoke-test.sh` then starts a published binary from outside its folder, with a temporary database and a fake browser command that captures the one-time launch link. It exchanges the link for a browser session, reads `GET /api/session`, checks that `GET /` returns the bundled UI, prints `GET /api/credentials/status`, asks for exit through `POST /api/lifecycle/exit` and expects the process to end with exit code 0.

| Archive | Where it ran | Smoke test | Credential store seen by the smoke test |
|---|---|---|---|
| `osx-arm64` | macOS 26, Apple silicon | passed | macOS Keychain |
| `osx-x64` | same machine under Rosetta 2 | passed | macOS Keychain |
| `linux-arm64` | Docker, `ubuntu:24.04` and `debian:bookworm-slim` | passed | encrypted vault, because `libsecret-1-0` is not installed |
| `linux-arm64` | Docker, `ubuntu:24.04` with `libsecret-1-0`, `gnome-keyring` and a D-Bus session | passed | Secret Service (libsecret) |
| `linux-x64` | Docker, `ubuntu:24.04` with `--platform linux/amd64` | passed | encrypted vault |
| `win-x64` | cross-built on macOS; started only by the `release` workflow on `windows-latest` | not run locally | not checked locally |
| `win-arm64` | cross-built on macOS | not run | not checked |

The Windows archives were checked for their contents (`TermForge.exe`, `wwwroot/index.html`). No Windows machine was available, so the only start of `win-x64` is the smoke test of the `release` workflow, which blocks the release when it fails. `win-arm64` is built but not verified.

Linux needs ICU: in a container without `libicu` the runtime stops at start. [installation.md](installation.md#linux) lists the packages.

## Credential store per platform

| Platform | Automated | Manual |
|---|---|---|
| macOS | `OperatingSystemCredentialStoreTests` round-trip against the real login keychain on the test machine | smoke test reports `OperatingSystemStore`, macOS Keychain |
| Linux | the same tests report inconclusive with the reason when no Secret Service is reachable, as on CI runners; `EncryptedVaultTests` and `CredentialBackendSelectorTests` cover the vault and the fallback | smoke test with gnome-keyring reports Secret Service; without libsecret it reports the vault and the install hint |
| Windows | not run for 1.1.0: CI no longer runs on Windows. The round-trip last passed against Windows Credential Manager for 1.0.0 | the `release` workflow smoke test on `windows-latest` |

## Acceptance criteria

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | Launch the application on Windows, macOS and Linux | met on x64 and arm64 for macOS and Linux; Windows x64 is checked by the `release` workflow smoke test; Windows arm64 not verified | smoke tests above; `ApplicationExitTests`, `ApplicationShutdownTests`, `GracefulShutdownTests`, `LoopbackBindingStartupTests`, `HostStartupTests` |
| 2 | Connect to saved SSH servers with host key verification | met | `HostKeyTrustFlowTests` (unknown key accepted once or saved, rejected, changed key blocked and replaced only after confirmation, revoked key, jump host prompts per hop), `HostKeyVerifierTests`, `KnownHostsLineParserTests`, `SavedConnectionServiceTests`, `SavedConnectionEndpointsTests`, `HostKeyDialog.test.tsx`, `ServerForm.test.tsx` |
| 3 | Use multiple independent interactive terminal sessions | met | `TerminalSessionManagerTests.ConcurrentSessions_HaveSeparatePtysAndShellState`, `OpenedSession_ExchangesInputAndOutputAndResizeReachesRemotePty`, `TerminalSessionTests`, `TerminalEndpointsTests`, `TerminalWorkspace.test.tsx` |
| 4 | Upload, download and edit remote files with conflict protection | met | `TransferQueueTests` (integrity by hash, cancel, interrupted then resumed, overwrite decision), `RemoteFileEditorTests.Save_AfterExternalModification_ReturnsConflictAndKeepsServerContent`, `RemoteFileSaverTests`, `SudoFileEditorTests`, `EditorEndpointsTests`, `EditorWorkspace.test.tsx`, `TransferPanel.test.tsx` |
| 5 | Perform basic Docker operations and open a shell inside a running container | met by unit, endpoint and UI tests | `ContainerActionServiceTests`, `ContainerShellLauncherTests`, `ContainerLogServiceTests`, `DockerContainerListParserTests`, `DockerEndpointsTests`, `DockerCommandIntegrationTests`, `DockerWorkspace.test.tsx`. The integration tests reach a real SSH server but no Docker daemon behind it, so container actions and the container shell run against a substituted command runner |
| 6 | Restore workspace state after restarting the application | met | `WorkspaceStateStoreTests`, `WorkspaceStateLoaderTests`, `WorkspaceStateUpgraderTests`, `WorkspaceEndpointsTests`, `WorkspaceRestore.test.tsx`, `TerminalWorkspace.restore.test.tsx`, `EditorWorkspace.restore.test.tsx` |
| 7 | Store credentials securely and export them only in an encrypted file | met, with the vault limitation below | `OperatingSystemCredentialStoreTests`, `EncryptedVaultTests`, `SecretTests`, `SecretLoggingTests`, `SettingsPackageCipherTests`, `ExportKeyTests`, `SettingsTransferEndpointsTests.Export_NeverWritesPlaintextSecretsToTheFilesTheDatabaseOrTheLogs`, `Export_ThenImport_PutsThePasswordsIntoTheCredentialStore`, `ExportSettingsDialog.test.tsx`; smoke tests for the store per platform |
| 8 | Receive actionable diagnostics for unavailable features | met | `CapabilityDiagnosticsServiceTests`, `CapabilityProbeTests`, `DiagnosticsEndpointsTests`, `DockerFailureClassifierTests`, `DockerCommandIntegrationTests.ContainerList_OnServerWithoutDocker_ReportsDockerMissing`, `ServerDiagnostics.test.tsx`, `CredentialBackendSelectorTests` (store diagnostics) |
| 9 | Run automated tests covering the critical workflows | met | see [Test run](#test-run) |

## Changes since 1.0.0

| Change | Evidence |
|---|---|
| Docker images (list, remove, pull), container remove, compose restart and recreate, multi-select | `ComposeActionServiceTests`, `ComposeLabelParserTests`, `DockerImageListParserTests`, `DockerImageServicesTests`, `DockerReferenceValidationTests`, `DockerCommandsTests`, `DockerEndpointsTests` (`Remove_WithConfirmation_ForceRemovesContainer`, `ComposeRestart_WithoutConfirmation_IsConflict`, `ImageRemove_WhenInUse_IsConflict`, `Pull_StreamsProgressAndRecordsOperation`), `ImagesPanel.test.tsx`, `useRowSelection.test.ts`, `DockerConfirmationsSection.test.tsx`, `dockerConfirmPreferences.test.ts` |
| AI bridge (MCP server, AI access flag, approvals) | `AiBridgeEndpointsTests` (`Mcp_WhenBridgeDisabled_IsNotFound`, `Mcp_WithoutToken_IsUnauthorized`, `Mcp_WithReplacedToken_IsUnauthorized`, `Mcp_FromAnotherOrigin_IsForbidden`, `ListServers_ReturnsOnlyServersWithAiAccess`, `RunCommand_WaitsForApprovalInTheBrowser`), `AiActionGateTests`, `AiApprovalBrokerTests`, `AiServerAccessPolicyTests`, `AiFileWriterTests`, `AiContainerActionServiceTests`, `AiAccessPersistenceTests`, `AiBridgeSettingsStoreTests`, `AiApprovalDialog.test.tsx`, `AiBridgeSection.test.tsx` |
| Latency, load average and CPU count metrics | `MetricsOutputParserTests`, `ServerMonitoringServiceTests`, `MonitoringEndpointsTests`, `ServerMetricsSection.test.tsx`, `metricsFormat.test.ts` |
| Show saved server password | `SavedConnectionEndpointsTests.RevealPassword_WithStoredPassword_ReturnsItWithoutCaching`, `RevealPassword_WithoutStoredPassword_ReturnsNotFound` |
| Settings export as one encrypted file with a generated key | `SettingsPackageCipherTests`, `ExportKeyTests`, `SettingsTransferEndpointsTests` |
| Collapsed server rail and server filter | `ServerSidebar.test.tsx`, `serverInitials.test.ts` |
| Control, dialog and loading motion | `motion.test.ts`, `LoadingState.test.tsx` |

## Test run

Results of the verification batch on the release commit:

| Suite | Result |
|---|---|
| `dotnet test termforge.slnx` | 1765 passed, 0 failed, 0 skipped: Core 999, Entrypoint 462, Ssh.IntegrationTests 123, HostKeys 70, Credentials 56, Persistence 55 |
| `npm test` in `src/Web` | 430 passed in 66 files |
| `npm run lint`, `npm run typecheck`, `npm run build` | passed |

On the first run all Ssh.IntegrationTests failed because Docker could not download Alpine packages while it built the test server image (`apk add` exit code 2). Package access worked a few minutes later and the rerun of the suite passed; no code changed in between.

The SSH integration tests ran against OpenSSH servers in Docker. CI runs every suite on `ubuntu-latest` only.

## Known limitations of 1.1.0

- The Windows builds are not started before the `release` workflow: `win-x64` is smoke-tested there, `win-arm64` is not verified.
- CI runs on Linux only, so the macOS Keychain and Windows Credential Manager round-trip tests run only on developer machines.
- The encrypted vault has no unlock screen. Users without a working system credential store unlock it from the browser developer console, see [credentials.md](credentials.md).
- macOS builds are not notarized. A downloaded copy needs the quarantine attribute removed, see [installation.md](installation.md#macos).
- There is no `linux-musl` build.
- Deferred by the specification: two-way directory sync, full Docker administration, remote access by default, free panel layout, plugins and automatic updates.
