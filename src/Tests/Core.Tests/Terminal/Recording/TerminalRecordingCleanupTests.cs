using DTech.Core.Terminal.Recording;
using DTech.Core.Tests.Terminal.Recording.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Terminal.Recording;

[TestClass]
public sealed class TerminalRecordingCleanupTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Factory_CancelledStart_ClosesCastAndRemovesOwnedFiles()
    {
        await using var graph = new RecordingTestGraph();
        var metadata = graph.CreateMetadata(RecordingTestGraph.StartTime.UtcDateTime);
        var setup = new TerminalRecorderSetup(metadata, 1024 * 1024);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var start = () => graph.RecorderFactory.StartAsync(setup, cancellation.Token);

        await start.Should().ThrowAsync<OperationCanceledException>();

        Directory.GetFiles(graph.Storage.DirectoryPath).Should().BeEmpty();
        var retried = await graph.RecorderFactory.StartAsync(setup, TestContext.CancellationToken);
        retried.IsSuccess.Should().BeTrue();
        await graph.RecorderFactory.DiscardAsync(retried.Value);
    }

    [TestMethod]
    public async Task Factory_CreateNewCollision_DoesNotDeleteTheExistingRecording()
    {
        await using var graph = new RecordingTestGraph();
        var existing = await graph.StoreFinishedRecordingAsync(RecordingTestGraph.StartTime.UtcDateTime);
        var path = CastPath(graph, existing.Id);
        var original = await File.ReadAllBytesAsync(path, TestContext.CancellationToken);
        var setup = new TerminalRecorderSetup(existing, 1024 * 1024);

        var started = await graph.RecorderFactory.StartAsync(setup, TestContext.CancellationToken);

        started.IsSuccess.Should().BeFalse();
        started.Error!.Code.Should().Be(TerminalRecordingErrorCode.StorageFailed);
        var content = await File.ReadAllBytesAsync(path, TestContext.CancellationToken);
        content.Should().Equal(original);
        var stored = await graph.Storage.FindAsync(existing.Id, TestContext.CancellationToken);
        stored.Should().Be(existing);
    }

    [TestMethod]
    public async Task Metadata_CancelledReplacement_RemovesTemporaryFileAndPreservesOriginal()
    {
        await using var graph = new RecordingTestGraph();
        var existing = await graph.StoreFinishedRecordingAsync(RecordingTestGraph.StartTime.UtcDateTime);
        var changed = existing with { ConnectionName = "replacement" };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var write = () => graph.Storage.WriteMetadataAsync(changed, cancellation.Token);

        await write.Should().ThrowAsync<OperationCanceledException>();

        var stored = await graph.Storage.FindAsync(existing.Id, TestContext.CancellationToken);
        stored.Should().Be(existing);
        var temporary = Path.Combine(graph.Storage.DirectoryPath, existing.Id.Value + ".json.tmp");
        File.Exists(temporary).Should().BeFalse();
    }

    [TestMethod]
    public async Task StartupCleanup_RemovesKnownOrphansAndKeepsPairsAndUnknownFiles()
    {
        await using var graph = new RecordingTestGraph();
        var complete = await graph.StoreFinishedRecordingAsync(RecordingTestGraph.StartTime.UtcDateTime);
        var orphan = TerminalRecordingId.Create();
        await using (var stream = graph.Storage.CreateCast(orphan))
        {
            var content = "partial"u8.ToArray();
            await stream.WriteAsync(content, TestContext.CancellationToken);
        }

        var temporaryId = TerminalRecordingId.Create();
        var temporary = Path.Combine(graph.Storage.DirectoryPath, temporaryId.Value + ".json.tmp");
        var unknown = Path.Combine(graph.Storage.DirectoryPath, "unknown.cast");
        await File.WriteAllTextAsync(temporary, "partial metadata", TestContext.CancellationToken);
        await File.WriteAllTextAsync(unknown, "keep", TestContext.CancellationToken);

        var removed = graph.Storage.DeleteOrphanFiles(TestContext.CancellationToken);

        removed.Should().Be(2);
        var orphanPath = CastPath(graph, orphan);
        var completePath = CastPath(graph, complete.Id);
        File.Exists(orphanPath).Should().BeFalse();
        File.Exists(temporary).Should().BeFalse();
        File.Exists(completePath).Should().BeTrue();
        File.Exists(unknown).Should().BeTrue();
        var stored = await graph.Storage.FindAsync(complete.Id, TestContext.CancellationToken);
        stored.Should().Be(complete);
    }

    [TestMethod]
    public async Task RuntimeRetention_DoesNotScanAnUnpublishedCastAsAnOrphan()
    {
        await using var graph = new RecordingTestGraph();
        var id = TerminalRecordingId.Create();
        await using var cast = graph.Storage.CreateCast(id);
        var settings = TerminalRecordingSettings.Default;

        var removed = await graph.Retention.PruneAsync(settings, TestContext.CancellationToken);

        removed.Should().Be(0);
        var path = CastPath(graph, id);
        File.Exists(path).Should().BeTrue();
    }

    [TestMethod]
    public async Task StartingRecording_IsProtectedFromRetentionAndLibraryDeletionUntilHandoff()
    {
        await using var graph = new RecordingTestGraph();
        var expiredAt = RecordingTestGraph.StartTime.UtcDateTime.AddDays(-40);
        var starting = await graph.StoreFinishedRecordingAsync(expiredAt);
        var settings = TerminalRecordingSettings.Default with { RetentionDays = 1 };
        using (graph.Registry.ProtectStart(starting.Id))
        {
            var pruned = await graph.Retention.PruneAsync(settings, TestContext.CancellationToken);
            var deleted = await graph.Library.DeleteAsync(starting.Id.Value, TestContext.CancellationToken);

            pruned.Should().Be(0);
            deleted.Error!.Code.Should().Be(TerminalRecordingErrorCode.StillRecording);
        }

        var afterHandoff = await graph.Retention.PruneAsync(settings, TestContext.CancellationToken);
        afterHandoff.Should().Be(1);
    }

    [TestMethod]
    public async Task StopThenClose_ForgetsTheLatestCompletedRecorder()
    {
        await using var graph = new RecordingTestGraph();
        graph.EnableRecording();
        var session = await graph.OpenSessionAsync("server");
        var started = await graph.RecordingService.StartAsync(session, TestContext.CancellationToken);

        await graph.RecordingService.StopAsync(session);
        graph.RecordingService.FindLatest(session).Should().BeSameAs(started.Value);
        await graph.SessionManager.ReleaseAsync(session, "closed after recording stopped");

        graph.RecordingService.FindLatest(session).Should().BeNull();
    }

    [TestMethod]
    public async Task ClosingRecorder_RemainsProtectedAndAwaitedUntilMetadataFinishes()
    {
        await using var graph = new RecordingTestGraph();
        graph.EnableRecording();
        var session = await graph.OpenSessionAsync("server");
        var timeProvider = new GatedEndTimeProvider();
        var factory = new TerminalRecorderFactory(graph.Storage, timeProvider);
        var service = new TerminalRecordingService(graph.SettingsService, factory, graph.Registry, graph.Retention, graph.TimeProvider);
        var started = await service.StartAsync(session, TestContext.CancellationToken);
        try
        {
            await graph.SessionManager.ReleaseAsync(session, "closed while finalizing metadata");
            var timeout = TimeSpan.FromSeconds(5);
            await timeProvider.Waiting.WaitAsync(timeout, TestContext.CancellationToken);

            graph.RecordingService.FindLatest(session).Should().BeNull();
            graph.Registry.IsRecording(started.Value.Id).Should().BeTrue();
            var stopping = graph.Registry.StopAllAsync(TerminalRecordingEndReason.SessionClosed);
            stopping.IsCompleted.Should().BeFalse();

            timeProvider.Resume();
            await stopping;
            graph.Registry.IsRecording(started.Value.Id).Should().BeFalse();
        }
        finally
        {
            timeProvider.Resume();
            await started.Value.Completion;
        }
    }

    [TestMethod]
    public async Task RepeatedStartStop_CloseStopsAndForgetsTheCurrentRecorder()
    {
        await using var graph = new RecordingTestGraph();
        graph.EnableRecording();
        var session = await graph.OpenSessionAsync("server");
        for (var i = 0; i < 5; i++)
        {
            var started = await graph.RecordingService.StartAsync(session, TestContext.CancellationToken);
            started.IsSuccess.Should().BeTrue();
            await graph.RecordingService.StopAsync(session);
        }

        var latest = await graph.RecordingService.StartAsync(session, TestContext.CancellationToken);
        await graph.SessionManager.ReleaseAsync(session, "closed while recording");
        await latest.Value.Completion;

        latest.Value.Metadata.EndReason.Should().Be(TerminalRecordingEndReason.SessionClosed);
        graph.RecordingService.FindLatest(session).Should().BeNull();
        graph.Registry.ListActive().Should().BeEmpty();
    }

    [TestMethod]
    public async Task CloseDuringStart_CancelsTheFactoryAndLeavesNoFilesOrRegistryEntry()
    {
        await using var graph = new RecordingTestGraph();
        graph.EnableRecording();
        var session = await graph.OpenSessionAsync("server");
        Task closing = Task.CompletedTask;
        Action close = () => closing = graph.SessionManager.ReleaseAsync(session, "closed during start");
        var timeProvider = new TimestampCallbackTimeProvider(close);
        var factory = new TerminalRecorderFactory(graph.Storage, timeProvider);
        var service = new TerminalRecordingService(graph.SettingsService, factory, graph.Registry, graph.Retention, graph.TimeProvider);
        var succeeded = false;

        try
        {
            var started = await service.StartAsync(session, TestContext.CancellationToken);
            succeeded = started.IsSuccess;
        }
        catch (OperationCanceledException)
        {
        }

        await closing;

        succeeded.Should().BeFalse();
        Directory.GetFiles(graph.Storage.DirectoryPath).Should().BeEmpty();
        graph.RecordingService.FindLatest(session).Should().BeNull();
        graph.Registry.ListActive().Should().BeEmpty();
    }

    [TestMethod]
    public async Task CancellationDuringRetention_DetachesStopsAndDeletesTheUnsuccessfulStart()
    {
        await using var graph = new RecordingTestGraph();
        graph.EnableRecording();
        var session = await graph.OpenSessionAsync("server");
        using var cancellation = new CancellationTokenSource();
        Action cancel = cancellation.Cancel;
        var timeProvider = new ClockCallbackTimeProvider(cancel);
        var retention = new TerminalRecordingRetention(graph.Storage, graph.Registry, timeProvider);
        var service = new TerminalRecordingService(graph.SettingsService, graph.RecorderFactory, graph.Registry, retention, graph.TimeProvider);
        var start = () => service.StartAsync(session, cancellation.Token);

        await start.Should().ThrowAsync<OperationCanceledException>();

        Directory.GetFiles(graph.Storage.DirectoryPath).Should().BeEmpty();
        graph.RecordingService.FindLatest(session).Should().BeNull();
        graph.Registry.ListActive().Should().BeEmpty();
        var retried = await graph.RecordingService.StartAsync(session, TestContext.CancellationToken);
        retried.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public async Task ConcurrentStarts_KeepOnlyOneRecorderAndNoFilesFromTheRejectedAttempt()
    {
        await using var graph = new RecordingTestGraph();
        graph.EnableRecording();
        var session = await graph.OpenSessionAsync("server");

        var first = graph.RecordingService.StartAsync(session, TestContext.CancellationToken);
        var second = graph.RecordingService.StartAsync(session, TestContext.CancellationToken);
        var results = await Task.WhenAll(first, second);

        results.Count(result => result.IsSuccess).Should().Be(1);
        Directory.GetFiles(graph.Storage.DirectoryPath).Should().HaveCount(2);
        graph.Registry.ListActive().Should().ContainSingle();
    }

    [TestMethod]
    public async Task RegisterRacingWithClose_DoesNotRetainARecorderForAClosedSession()
    {
        await using var graph = new RecordingTestGraph();
        for (var i = 0; i < 8; i++)
        {
            var session = await graph.OpenSessionAsync("server");
            var metadata = graph.CreateMetadata(RecordingTestGraph.StartTime.UtcDateTime);
            var setup = new TerminalRecorderSetup(metadata, 1024 * 1024);
            var started = await graph.RecorderFactory.StartAsync(setup, TestContext.CancellationToken);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var registering = Task.Run(async () =>
            {
                await gate.Task;
                graph.Registry.TryRegister(session, started.Value);
            });
            var closing = Task.Run(async () =>
            {
                await gate.Task;
                await graph.SessionManager.ReleaseAsync(session, "concurrent close");
            });
            gate.SetResult();
            var completed = Task.WhenAll(registering, closing);
            var timeout = TimeSpan.FromSeconds(5);

            await completed.WaitAsync(timeout, TestContext.CancellationToken);
            await graph.RecorderFactory.DiscardAsync(started.Value);

            graph.RecordingService.FindLatest(session).Should().BeNull();
            graph.Registry.ListActive().Should().BeEmpty();
        }
    }

    [TestMethod]
    public async Task ForgetRacingWithClose_CompletesWithoutWaitingInsideItsOwnCallback()
    {
        await using var graph = new RecordingTestGraph();
        graph.EnableRecording();
        for (var i = 0; i < 8; i++)
        {
            var session = await graph.OpenSessionAsync("server");
            var started = await graph.RecordingService.StartAsync(session, TestContext.CancellationToken);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var forgetting = Task.Run(async () =>
            {
                await gate.Task;
                graph.Registry.Forget(session.Descriptor.Id, started.Value);
            });
            var closing = Task.Run(async () =>
            {
                await gate.Task;
                await graph.SessionManager.ReleaseAsync(session, "concurrent forget");
            });
            gate.SetResult();
            var completed = Task.WhenAll(forgetting, closing);
            var timeout = TimeSpan.FromSeconds(5);

            await completed.WaitAsync(timeout, TestContext.CancellationToken);
            await started.Value.StopAsync(TerminalRecordingEndReason.Stopped);

            graph.RecordingService.FindLatest(session).Should().BeNull();
        }
    }

    private static string CastPath(RecordingTestGraph graph, TerminalRecordingId id)
    {
        return Path.Combine(graph.Storage.DirectoryPath, id.Value + AsciicastFormat.FileExtension);
    }

    private sealed class TimestampCallbackTimeProvider(Action callback) : TimeProvider
    {
        public override long GetTimestamp()
        {
            callback();
            return TimeProvider.System.GetTimestamp();
        }
    }

    private sealed class ClockCallbackTimeProvider(Action callback) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            callback();
            return RecordingTestGraph.StartTime;
        }
    }

    private sealed class GatedEndTimeProvider : TimeProvider
    {
        private readonly TaskCompletionSource _waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _resume = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Waiting => _waiting.Task;

        public void Resume()
        {
            _resume.TrySetResult();
        }

        public override DateTimeOffset GetUtcNow()
        {
            _waiting.TrySetResult();
            _resume.Task.GetAwaiter().GetResult();
            return RecordingTestGraph.StartTime;
        }
    }
}
