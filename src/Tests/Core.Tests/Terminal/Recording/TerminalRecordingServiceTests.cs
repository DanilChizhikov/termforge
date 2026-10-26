using System.Text;
using DTech.Core.Terminal.Recording;
using DTech.Core.Tests.Terminal.Recording.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Terminal.Recording;

[TestClass]
public sealed class TerminalRecordingServiceTests
{
    private const long OneMegabyte = 1024 * 1024;
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Settings_AreOffByDefault()
    {
        await using var graph = new RecordingTestGraph();

        var settings = await graph.SettingsService.GetAsync(TestContext.CancellationToken);

        settings.Should().Be(TerminalRecordingSettings.Default);
        settings.Enabled.Should().BeFalse();
    }

    [TestMethod]
    public async Task Start_WhileRecordingIsOff_IsRejectedAndWritesNothing()
    {
        await using var graph = new RecordingTestGraph();
        var session = await graph.OpenSessionAsync("web");

        var started = await graph.RecordingService.StartAsync(session, TestContext.CancellationToken);

        started.IsSuccess.Should().BeFalse();
        started.Error!.Code.Should().Be(TerminalRecordingErrorCode.Disabled);
        Directory.Exists(graph.Storage.DirectoryPath).Should().BeFalse();
        graph.RecordingService.FindLatest(session).Should().BeNull();
    }

    [TestMethod]
    public async Task Start_RecordsOnlyTheSessionItWasEnabledFor()
    {
        await using var graph = new RecordingTestGraph();
        graph.EnableRecording();
        var recorded = await graph.OpenSessionAsync("web");
        var other = await graph.OpenSessionAsync("db");

        var started = await graph.RecordingService.StartAsync(recorded, TestContext.CancellationToken);
        var headerSize = started.Value.BytesWritten;
        graph.RemoteSessions[0].Emit("recorded output");
        graph.RemoteSessions[1].Emit("other output");
        await RecordingTestGraph.WaitUntilAsync(() => started.Value.BytesWritten > headerSize);
        await graph.RecordingService.StopAsync(recorded);

        var recordings = await graph.Library.ListAsync(TestContext.CancellationToken);
        recordings.Should().ContainSingle();
        var content = await ReadCastAsync(graph, started.Value.Id);
        content.Should().Contain("recorded output");
        content.Should().NotContain("other output");
        graph.RecordingService.FindLatest(other).Should().BeNull();
    }

    [TestMethod]
    public async Task Start_NeverRecordsInput()
    {
        await using var graph = new RecordingTestGraph();
        graph.EnableRecording();
        var session = await graph.OpenSessionAsync("web");
        var started = await graph.RecordingService.StartAsync(session, TestContext.CancellationToken);
        var headerSize = started.Value.BytesWritten;

        var input = Encoding.UTF8.GetBytes("typed-password");
        await session.WriteAsync(input, TestContext.CancellationToken);
        graph.RemoteSessions[0].Emit("prompt$ ");
        await RecordingTestGraph.WaitUntilAsync(() => started.Value.BytesWritten > headerSize);
        await graph.RecordingService.StopAsync(session);

        var content = await ReadCastAsync(graph, started.Value.Id);
        content.Should().Contain("prompt$ ");
        content.Should().NotContain("typed-password");
    }

    [TestMethod]
    public async Task Start_WhileAlreadyRecording_IsRejected()
    {
        await using var graph = new RecordingTestGraph();
        graph.EnableRecording();
        var session = await graph.OpenSessionAsync("web");
        await graph.RecordingService.StartAsync(session, TestContext.CancellationToken);

        var second = await graph.RecordingService.StartAsync(session, TestContext.CancellationToken);

        second.Error!.Code.Should().Be(TerminalRecordingErrorCode.AlreadyRecording);
    }

    [TestMethod]
    public async Task Stop_FinishesTheRecordingAndAllowsAnotherOne()
    {
        await using var graph = new RecordingTestGraph();
        graph.EnableRecording();
        var session = await graph.OpenSessionAsync("web");
        var first = await graph.RecordingService.StartAsync(session, TestContext.CancellationToken);

        var stopped = await graph.RecordingService.StopAsync(session);
        var second = await graph.RecordingService.StartAsync(session, TestContext.CancellationToken);

        stopped.Value.IsActive.Should().BeFalse();
        stopped.Value.Metadata.EndReason.Should().Be(TerminalRecordingEndReason.Stopped);
        stopped.Value.Metadata.IsTruncated.Should().BeFalse();
        second.IsSuccess.Should().BeTrue();
        second.Value.Id.Should().NotBe(first.Value.Id);
    }

    [TestMethod]
    public async Task Stop_WhenNotRecording_IsRejected()
    {
        await using var graph = new RecordingTestGraph();
        graph.EnableRecording();
        var session = await graph.OpenSessionAsync("web");

        var stopped = await graph.RecordingService.StopAsync(session);

        stopped.Error!.Code.Should().Be(TerminalRecordingErrorCode.NotRecording);
    }

    [TestMethod]
    public async Task ClosingTheSession_FinishesTheRecording()
    {
        await using var graph = new RecordingTestGraph();
        graph.EnableRecording();
        var session = await graph.OpenSessionAsync("web");
        var started = await graph.RecordingService.StartAsync(session, TestContext.CancellationToken);

        await graph.SessionManager.ReleaseAsync(session, "closed by user");
        await started.Value.Completion;

        var stored = await graph.Storage.FindAsync(started.Value.Id, TestContext.CancellationToken);
        stored!.EndReason.Should().Be(TerminalRecordingEndReason.SessionClosed);
        stored.EndedAt.Should().NotBeNull();
    }

    [TestMethod]
    public async Task TurningRecordingOff_StopsActiveRecordings()
    {
        await using var graph = new RecordingTestGraph();
        graph.EnableRecording();
        var session = await graph.OpenSessionAsync("web");
        var started = await graph.RecordingService.StartAsync(session, TestContext.CancellationToken);

        var disabled = TerminalRecordingSettings.Default with { Enabled = false };
        var updated = await graph.SettingsService.UpdateAsync(disabled, TestContext.CancellationToken);

        updated.IsSuccess.Should().BeTrue();
        started.Value.IsActive.Should().BeFalse();
        started.Value.Metadata.EndReason.Should().Be(TerminalRecordingEndReason.RecordingDisabled);
    }

    [TestMethod]
    public async Task ReachingTheSizeLimit_StopsTheRecordingButNotTheTerminal()
    {
        await using var graph = new RecordingTestGraph();
        graph.EnableRecording(maxSizeMegabytes: 1);
        var session = await graph.OpenSessionAsync("web");
        var started = await graph.RecordingService.StartAsync(session, TestContext.CancellationToken);

        var chunk = new string('x', 64 * 1024);
        for (var i = 0; i < 20; i++)
        {
            graph.RemoteSessions[0].Emit(chunk);
        }

        await started.Value.Completion;

        started.Value.Metadata.EndReason.Should().Be(TerminalRecordingEndReason.SizeLimitReached);
        started.Value.Metadata.IsTruncated.Should().BeTrue();
        var castPath = Path.Combine(graph.Storage.DirectoryPath, started.Value.Id.Value + AsciicastFormat.FileExtension);
        var castLength = new FileInfo(castPath).Length;
        castLength.Should().BeLessThanOrEqualTo(OneMegabyte);
        castLength.Should().BeGreaterThan(OneMegabyte - (100 * 1024));
        started.Value.Metadata.SizeBytes.Should().Be(castLength);
        session.IsClosed.Should().BeFalse();
    }

    [TestMethod]
    public async Task UpdateSettings_RejectsValuesOutsideTheLimits()
    {
        await using var graph = new RecordingTestGraph();
        var invalid = TerminalRecordingSettings.Default with { MaxRecordingSizeMegabytes = 0 };

        var updated = await graph.SettingsService.UpdateAsync(invalid, TestContext.CancellationToken);

        updated.Error!.Code.Should().Be(TerminalRecordingErrorCode.InvalidSettings);
        graph.SettingsStore.Stored.Should().BeNull();
    }

    private async Task<string> ReadCastAsync(RecordingTestGraph graph, TerminalRecordingId id)
    {
        var opened = await graph.Library.OpenAsync(id.Value, TestContext.CancellationToken);
        await using var cast = opened.Value;
        using var reader = new StreamReader(cast);
        return await reader.ReadToEndAsync(TestContext.CancellationToken);
    }
}
