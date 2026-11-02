using System.Net;
using System.Text.Json;
using DTech.Core.CommandHistory;
using DTech.Core.Credentials;
using DTech.Core.SavedConnections;
using DTech.Core.SettingsTransfer.Import;
using DTech.Core.SettingsTransfer.Secrets;
using DTech.Core.Terminal;
using DTech.Core.Terminal.Recording;
using DTech.Core.Workspace;
using DTech.Entrypoint.Tests.SettingsTransfer.Support;
using DTech.Persistence.CommandHistory;
using DTech.Persistence.Database;
using DTech.Persistence.Workspace;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DTech.Entrypoint.Tests.SettingsTransfer;

[TestClass]
public sealed class AtomicSettingsImportTests
{
    private const string ImportPath = "/api/settings-transfer/import";
    private const string NewPassword = "new-import-password";
    private const string OldPassword = "original-password";
    private const string OldSudoPassword = "original-sudo-password";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Import_LateDatabaseFailure_RollsBackAllSectionsAndSecretsWithoutPruningRecordings()
    {
        await using var source = await SettingsTransferApiHarness.StartAsync(TestContext);
        var failure = new FailPinnedSaveInterceptor();
        await using var target = await SettingsTransferApiHarness.StartAsync(TestContext, services =>
            services.AddDbContext<TermForgeDbContext>(options => options.AddInterceptors(failure)));
        var sourceId = await SeedSourceAsync(source);
        var targetId = await SeedTargetAsync(target, true);
        var beforeConnections = await target.ListConnectionsAsync();
        var beforeWorkspace = await GetAsync(target, "/api/workspace");
        var beforePreferences = await GetAsync(target, "/api/recordings/settings");
        var beforeExclusions = await GetAsync(target, "/api/command-history/exclusions");
        var recording = await StoreOldRecordingAsync(target);
        var export = await ExportAsync(source);
        var body = ImportBody(export);
        var resolutions = new Dictionary<Guid, string> { [sourceId] = "useImported" };
        body["resolutions"] = resolutions;
        failure.Enabled = true;

        using var response = await target.SendAsync(HttpMethod.Post, ImportPath, body);
        var problem = await target.ReadJsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        problem.GetProperty("code").GetString().Should().Be("ImportFailed");
        (await target.ListConnectionsAsync()).GetRawText().Should().Be(beforeConnections.GetRawText());
        (await GetAsync(target, "/api/workspace")).GetRawText().Should().Be(beforeWorkspace.GetRawText());
        (await GetAsync(target, "/api/recordings/settings")).GetRawText().Should().Be(beforePreferences.GetRawText());
        (await GetAsync(target, "/api/command-history/exclusions")).GetRawText().Should().Be(beforeExclusions.GetRawText());
        AssertOriginalSecrets(target, targetId);
        var storage = target.Factory.Services.GetRequiredService<TerminalRecordingStorage>();
        var remaining = await storage.FindAsync(recording.Id, TestContext.CancellationToken);
        remaining.Should().NotBeNull();
        failure.Attempts.Should().Be(1);
        using var scope = target.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICommandHistoryRepository>();
        var query = new CommandHistoryQuery(targetId, null, 100);
        var history = await repository.ListAsync(query, TestContext.CancellationToken);
        history.Should().BeEmpty();
        target.Logs.Messages.Should().NotContain(message => message.Contains(NewPassword) || message.Contains(OldPassword));
    }

    [TestMethod]
    public async Task Import_FailedNativeWriteWithUnknownOutcome_RestoresBothPasswordsAndReturnsFailure()
    {
        await using var source = await SettingsTransferApiHarness.StartAsync(TestContext);
        await using var target = await SettingsTransferApiHarness.StartAsync(TestContext);
        await SeedSourceAsync(source);
        var targetId = await SeedTargetAsync(target);
        var before = await target.ListConnectionsAsync();
        var export = await ExportAsync(source);
        var body = ImportBody(export);
        target.CredentialStore.FailStoreCall = target.CredentialStore.StoreCalls + 2;
        target.CredentialStore.MutateBeforeFailure = true;

        using var response = await target.SendAsync(HttpMethod.Post, ImportPath, body);
        var problem = await target.ReadJsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        problem.GetProperty("code").GetString().Should().Be("SecretsUnavailable");
        AssertOriginalSecrets(target, targetId);
        (await target.ListConnectionsAsync()).GetRawText().Should().Be(before.GetRawText());
    }

    [TestMethod]
    public async Task Import_FailedCompensation_IsAnExplicitError()
    {
        await using var source = await SettingsTransferApiHarness.StartAsync(TestContext);
        await using var target = await SettingsTransferApiHarness.StartAsync(TestContext);
        await SeedSourceAsync(source);
        await SeedTargetAsync(target);
        var export = await ExportAsync(source);
        var body = ImportBody(export);
        target.CredentialStore.FailStoreCall = target.CredentialStore.StoreCalls + 2;
        target.CredentialStore.MutateBeforeFailure = true;
        target.CredentialStore.FailCompensation = true;

        using var response = await target.SendAsync(HttpMethod.Post, ImportPath, body);
        var problem = await target.ReadJsonAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        problem.GetProperty("code").GetString().Should().Be("CompensationFailed");
        problem.GetProperty("detail").GetString().Should().Contain("could not be restored");
    }

    [TestMethod]
    public async Task Import_RollbackCannotOverwriteANormalPasswordSaveWaitingForTheMutationGate()
    {
        await using var source = await SettingsTransferApiHarness.StartAsync(TestContext);
        var reachedFailure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFailure = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failure = new FailPinnedSaveInterceptor { Reached = reachedFailure, WaitBeforeFailure = releaseFailure.Task };
        await using var target = await SettingsTransferApiHarness.StartAsync(TestContext, services =>
            services.AddDbContext<TermForgeDbContext>(options => options.AddInterceptors(failure)));
        await SeedSourceAsync(source);
        var targetId = await SeedTargetAsync(target);
        var export = await ExportAsync(source);
        var body = ImportBody(export);
        failure.Enabled = true;
        var importing = target.SendAsync(HttpMethod.Post, ImportPath, body);
        await reachedFailure.Task.WaitAsync(TestContext.CancellationToken);
        var writesBeforeNormalSave = target.CredentialStore.StoreCalls;
        var credentials = target.Factory.Services.GetRequiredService<ICredentialStore>();
        var key = PasswordCredentialKey.For(targetId);
        using var newest = Secret.FromString("newest-concurrent-password");

        var saving = credentials.Store(key, newest, TestContext.CancellationToken);
        var normalSaveWasBlocked = !saving.IsCompleted;
        var underlyingWasBlocked = target.CredentialStore.StoreCalls == writesBeforeNormalSave;
        releaseFailure.TrySetResult();
        using var response = await importing;
        var saved = await saving;

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        normalSaveWasBlocked.Should().BeTrue();
        underlyingWasBlocked.Should().BeTrue();
        saved.IsSuccess.Should().BeTrue(saved.Error?.Message);
        target.FindSecret(key).Should().Be("newest-concurrent-password");
        var sudoKey = ConnectionSecretKeys.For(targetId, ArchivedSecretKind.SudoPassword);
        target.FindSecret(sudoKey).Should().Be(OldSudoPassword);
    }

    [TestMethod]
    public async Task Import_CallerCancellationAfterFirstMutation_DoesNotInterruptApply()
    {
        await using var source = await SettingsTransferApiHarness.StartAsync(TestContext);
        await using var target = await SettingsTransferApiHarness.StartAsync(TestContext);
        await SeedSourceAsync(source);
        var export = await ExportAsync(source);
        var package = Convert.FromBase64String(export.GetProperty("file").GetProperty("contentBase64").GetString()!);
        var key = export.GetProperty("key").GetString()!;
        var sections = new DTech.Core.SettingsTransfer.SettingsSections(true, true, true, true);
        var options = new SettingsImportOptions(ImportMode.Merge, sections, []);
        var request = new SettingsImportRequest(package, key, options);
        using var cancellation = new CancellationTokenSource();
        target.CredentialStore.OnStore = token =>
        {
            token.CanBeCanceled.Should().BeFalse();
            cancellation.Cancel();
        };
        using var scope = target.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<SettingsImportService>();

        var result = await service.ApplyAsync(request, cancellation.Token);

        cancellation.IsCancellationRequested.Should().BeTrue();
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        result.Value.SecretsStored.Should().Be(2);
        result.Value.PinnedCommandsAdded.Should().Be(1);
        (await target.ListConnectionsAsync()).EnumerateArray().Should().ContainSingle();
    }

    [TestMethod]
    public async Task Import_HoldsWorkspaceCoordinatorWhileReadingDraftsAndPreservesALaterAutosave()
    {
        await using var source = await SettingsTransferApiHarness.StartAsync(TestContext);
        var pause = new WorkspaceReadPause();
        await using var target = await SettingsTransferApiHarness.StartAsync(TestContext, services =>
        {
            services.RemoveAll<IWorkspaceStateStore>();
            services.AddScoped<IWorkspaceStateStore>(provider =>
            {
                var context = provider.GetRequiredService<TermForgeDbContext>();
                var time = provider.GetRequiredService<TimeProvider>();
                var store = new WorkspaceStateStore(context, time);
                return new PausingWorkspaceStore(store, pause);
            });
        });
        await SeedSourceAsync(source);
        var targetId = await SeedTargetAsync(target);
        var export = await ExportAsync(source);
        var body = ImportBody(export);
        pause.Enabled = true;
        var importing = target.SendAsync(HttpMethod.Post, ImportPath, body);
        await pause.Read.Task.WaitAsync(TestContext.CancellationToken);
        using var scope = target.Factory.Services.CreateScope();
        var coordinator = scope.ServiceProvider.GetRequiredService<WorkspaceStateWriteCoordinator>();
        var saver = scope.ServiceProvider.GetRequiredService<WorkspaceStateSaver>();
        var draft = new WorkspaceEditorDraft(targetId, "/etc/app.conf", "newest autosaved draft", null);
        var state = new WorkspaceState { EditorTabs = [draft.ToTab()] };
        var snapshot = new WorkspaceSnapshot(state, [draft]);
        var autosaveEntered = false;
        var autosave = coordinator.RunAsync(async token =>
        {
            autosaveEntered = true;
            var saved = await saver.SaveWithinWriteAsync(snapshot, token);
            saved.IsSuccess.Should().BeTrue();
        }, TestContext.CancellationToken);

        var enteredBeforeImportReleased = autosaveEntered;
        pause.Release.TrySetResult();
        using var response = await importing;
        await autosave;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        enteredBeforeImportReleased.Should().BeFalse();
        autosaveEntered.Should().BeTrue();
        var store = scope.ServiceProvider.GetRequiredService<IWorkspaceStateStore>();
        var drafts = await store.LoadDraftsAsync(TestContext.CancellationToken);
        drafts.Should().Equal(draft);
    }

    private async Task<Guid> SeedSourceAsync(SettingsTransferApiHarness source)
    {
        var details = new { authMethod = "Password", password = NewPassword };
        var id = await source.CreateConnectionAsync("web", details);
        var sudoKey = ConnectionSecretKeys.For(id, ArchivedSecretKind.SudoPassword);
        source.CredentialStore.Put(sudoKey, "new-sudo-password");
        var settings = new { enabled = false, maxRecordingSizeMegabytes = 20, maxRecordings = 10, retentionDays = 7 };
        using var recording = await source.SendAsync(HttpMethod.Put, "/api/recordings/settings", settings);
        recording.StatusCode.Should().Be(HttpStatusCode.OK);
        var exclusions = new { patterns = new[] { "^custom-private " } };
        using var filters = await source.SendAsync(HttpMethod.Put, "/api/command-history/exclusions", exclusions);
        filters.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = new WorkspaceState { SelectedConnectionId = id, LocalPath = "/imported", ActiveView = "workspace" };
        var snapshot = new WorkspaceSnapshot(state, []);
        using var workspace = await source.SendAsync(HttpMethod.Put, "/api/workspace", snapshot);
        workspace.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = source.Factory.Services.CreateScope();
        var history = scope.ServiceProvider.GetRequiredService<ICommandHistoryRepository>();
        var command = new CommandHistoryEntry(Guid.NewGuid(), id, "uptime", DateTime.UtcNow, 0, true);
        await history.AddAsync(command, TestContext.CancellationToken);
        return id;
    }

    private async Task<Guid> SeedTargetAsync(SettingsTransferApiHarness target, bool differentHost = false)
    {
        var details = new { authMethod = "Password", password = OldPassword, host = differentHost ? "original.example.test" : "web.example.test" };
        var id = await target.CreateConnectionAsync("web", details);
        var sudoKey = ConnectionSecretKeys.For(id, ArchivedSecretKind.SudoPassword);
        target.CredentialStore.Put(sudoKey, OldSudoPassword);
        var draft = new WorkspaceEditorDraft(id, "/etc/app.conf", "unsaved content", null);
        var state = new WorkspaceState { LocalPath = "/original", EditorTabs = [draft.ToTab()] };
        var snapshot = new WorkspaceSnapshot(state, [draft]);
        using var workspace = await target.SendAsync(HttpMethod.Put, "/api/workspace", snapshot);
        workspace.StatusCode.Should().Be(HttpStatusCode.NoContent);
        return id;
    }

    private async Task<JsonElement> ExportAsync(SettingsTransferApiHarness source)
    {
        var sections = new { connections = true, preferences = true, workspace = true, pinnedCommands = true };
        var body = new { sections };
        using var response = await source.SendAsync(HttpMethod.Post, "/api/settings-transfer/export", body);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await source.ReadJsonAsync(response);
    }

    private static Dictionary<string, object?> ImportBody(JsonElement export)
    {
        return new Dictionary<string, object?>
        {
            ["file"] = export.GetProperty("file").GetProperty("contentBase64").GetString(),
            ["key"] = export.GetProperty("key").GetString(),
            ["mode"] = "merge",
            ["sections"] = new { connections = true, preferences = true, workspace = true, pinnedCommands = true },
        };
    }

    private static void AssertOriginalSecrets(SettingsTransferApiHarness target, Guid id)
    {
        var passwordKey = PasswordCredentialKey.For(id);
        var sudoKey = ConnectionSecretKeys.For(id, ArchivedSecretKind.SudoPassword);
        target.FindSecret(passwordKey).Should().Be(OldPassword);
        target.FindSecret(sudoKey).Should().Be(OldSudoPassword);
    }

    private async Task<JsonElement> GetAsync(SettingsTransferApiHarness target, string path)
    {
        using var response = await target.SendAsync(HttpMethod.Get, path, null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await target.ReadJsonAsync(response);
    }

    private async Task<TerminalRecordingMetadata> StoreOldRecordingAsync(SettingsTransferApiHarness target)
    {
        var storage = target.Factory.Services.GetRequiredService<TerminalRecordingStorage>();
        var metadata = new TerminalRecordingMetadata
        {
            Id = TerminalRecordingId.Create(),
            ConnectionId = Guid.NewGuid(),
            ConnectionName = "old",
            SessionNumber = 1,
            Size = new TerminalSize(80, 24),
            StartedAt = DateTime.UtcNow.AddDays(-90),
            EndedAt = DateTime.UtcNow.AddDays(-90),
            EndReason = TerminalRecordingEndReason.Stopped,
        };
        await using (var stream = storage.CreateCast(metadata.Id))
        {
            var header = AsciicastFormat.Header(metadata);
            await stream.WriteAsync(header, TestContext.CancellationToken);
        }

        await storage.WriteMetadataAsync(metadata, TestContext.CancellationToken);
        return metadata;
    }

    private sealed class FailPinnedSaveInterceptor : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }

        public int Attempts { get; private set; }

        public TaskCompletionSource? Reached { get; init; }

        public Task? WaitBeforeFailure { get; init; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var additions = eventData.Context!.ChangeTracker.Entries<CommandHistoryEntity>().Any(entry => entry.State == EntityState.Added);
            if (Enabled && additions)
            {
                Attempts++;
                Reached?.TrySetResult();
                if (WaitBeforeFailure is not null)
                {
                    await WaitBeforeFailure.WaitAsync(cancellationToken);
                }

                throw new InvalidOperationException("The pinned command write failed.");
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class WorkspaceReadPause
    {
        public bool Enabled { get; set; }

        public TaskCompletionSource Read { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class PausingWorkspaceStore(IWorkspaceStateStore store, WorkspaceReadPause pause) : IWorkspaceStateStore
    {
        public Task<StoredWorkspaceDocument?> LoadDocumentAsync(CancellationToken cancellationToken)
        {
            return store.LoadDocumentAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<WorkspaceEditorDraft>> LoadDraftsAsync(CancellationToken cancellationToken)
        {
            var drafts = await store.LoadDraftsAsync(cancellationToken);
            if (pause.Enabled)
            {
                pause.Read.TrySetResult();
                await pause.Release.Task.WaitAsync(cancellationToken);
            }

            return drafts;
        }

        public Task SaveAsync(StoredWorkspaceDocument document, IReadOnlyList<WorkspaceEditorDraft> drafts, CancellationToken cancellationToken)
        {
            return store.SaveAsync(document, drafts, cancellationToken);
        }

        public Task DeleteDocumentAsync(CancellationToken cancellationToken)
        {
            return store.DeleteDocumentAsync(cancellationToken);
        }
    }
}
