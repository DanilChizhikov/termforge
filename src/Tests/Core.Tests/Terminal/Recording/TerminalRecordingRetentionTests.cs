using DTech.Core.Terminal.Recording;
using DTech.Core.Tests.Terminal.Recording.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Terminal.Recording;

[TestClass]
public sealed class TerminalRecordingRetentionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Prune_DeletesRecordingsOlderThanTheRetentionPeriod()
    {
        await using var graph = new RecordingTestGraph();
        var now = RecordingTestGraph.StartTime.UtcDateTime;
        var expired = await graph.StoreFinishedRecordingAsync(now.AddDays(-31));
        var recent = await graph.StoreFinishedRecordingAsync(now.AddDays(-29));
        var settings = TerminalRecordingSettings.Default with { RetentionDays = 30 };

        var deleted = await graph.Retention.PruneAsync(settings, TestContext.CancellationToken);

        deleted.Should().Be(1);
        var remaining = await graph.Storage.ListAsync(TestContext.CancellationToken);
        remaining.Select(recording => recording.Id).Should().BeEquivalentTo([recent.Id]);
        File.Exists(Path.Combine(graph.Storage.DirectoryPath, expired.Id.Value + AsciicastFormat.FileExtension)).Should().BeFalse();
    }

    [TestMethod]
    public async Task Prune_KeepsOnlyTheNewestRecordingsUpToTheCount()
    {
        await using var graph = new RecordingTestGraph();
        var now = RecordingTestGraph.StartTime.UtcDateTime;
        var oldest = await graph.StoreFinishedRecordingAsync(now.AddHours(-3));
        var middle = await graph.StoreFinishedRecordingAsync(now.AddHours(-2));
        var newest = await graph.StoreFinishedRecordingAsync(now.AddHours(-1));
        var settings = TerminalRecordingSettings.Default with { MaxRecordings = 2 };

        await graph.Retention.PruneAsync(settings, TestContext.CancellationToken);

        var remaining = await graph.Storage.ListAsync(TestContext.CancellationToken);
        remaining.Select(recording => recording.Id).Should().BeEquivalentTo([middle.Id, newest.Id]);
        remaining.Should().NotContain(recording => recording.Id == oldest.Id);
    }

    [TestMethod]
    public async Task Prune_NeverDeletesARecordingInProgress()
    {
        await using var graph = new RecordingTestGraph();
        graph.EnableRecording();
        var session = await graph.OpenSessionAsync("web");
        var started = await graph.RecordingService.StartAsync(session, TestContext.CancellationToken);
        graph.TimeProvider.Now = RecordingTestGraph.StartTime.AddDays(400);
        var settings = TerminalRecordingSettings.Default with { MaxRecordings = 1, RetentionDays = 1 };

        var deleted = await graph.Retention.PruneAsync(settings, TestContext.CancellationToken);

        deleted.Should().Be(0);
        var remaining = await graph.Storage.ListAsync(TestContext.CancellationToken);
        remaining.Should().ContainSingle(recording => recording.Id == started.Value.Id);
    }

    [TestMethod]
    public async Task StartingARecording_AppliesTheCountLimit()
    {
        await using var graph = new RecordingTestGraph();
        var now = RecordingTestGraph.StartTime.UtcDateTime;
        await graph.StoreFinishedRecordingAsync(now.AddMinutes(-2));
        await graph.StoreFinishedRecordingAsync(now.AddMinutes(-1));
        graph.SettingsStore.Stored = TerminalRecordingSettings.Default with { Enabled = true, MaxRecordings = 2 };
        var session = await graph.OpenSessionAsync("web");

        var started = await graph.RecordingService.StartAsync(session, TestContext.CancellationToken);

        var remaining = await graph.Storage.ListAsync(TestContext.CancellationToken);
        remaining.Should().HaveCount(2);
        remaining.Should().Contain(recording => recording.Id == started.Value.Id);
    }
}
