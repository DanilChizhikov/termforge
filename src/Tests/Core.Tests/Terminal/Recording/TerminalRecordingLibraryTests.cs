using DTech.Core.Terminal.Recording;
using DTech.Core.Tests.Terminal.Recording.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Terminal.Recording;

[TestClass]
public sealed class TerminalRecordingLibraryTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Delete_RemovesTheCastAndItsMetadata()
    {
        await using var graph = new RecordingTestGraph();
        var stored = await graph.StoreFinishedRecordingAsync(RecordingTestGraph.StartTime.UtcDateTime);

        var deleted = await graph.Library.DeleteAsync(stored.Id.Value, TestContext.CancellationToken);

        deleted.IsSuccess.Should().BeTrue();
        Directory.EnumerateFiles(graph.Storage.DirectoryPath).Should().BeEmpty();
        var listed = await graph.Library.ListAsync(TestContext.CancellationToken);
        listed.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Delete_UnknownRecording_IsNotFound()
    {
        await using var graph = new RecordingTestGraph();
        var unknown = TerminalRecordingId.Create();

        var deleted = await graph.Library.DeleteAsync(unknown.Value, TestContext.CancellationToken);

        deleted.Error!.Code.Should().Be(TerminalRecordingErrorCode.NotFound);
    }

    [TestMethod]
    [DataRow("../termforge.db")]
    [DataRow("..\\..\\secrets")]
    [DataRow("/etc/passwd")]
    [DataRow("0123456789abcdef0123456789abcdeF")]
    [DataRow("0123456789abcdef0123456789abcde")]
    [DataRow("0123456789abcdef0123456789abcdef.json")]
    [DataRow("")]
    public async Task Delete_RejectsIdsThatAreNotRecordingIds(string id)
    {
        await using var graph = new RecordingTestGraph();
        var outside = graph.Directory.Write("termforge.db", "database");

        var deleted = await graph.Library.DeleteAsync(id, TestContext.CancellationToken);
        var opened = await graph.Library.OpenAsync(id, TestContext.CancellationToken);

        deleted.Error!.Code.Should().Be(TerminalRecordingErrorCode.InvalidId);
        opened.Error!.Code.Should().Be(TerminalRecordingErrorCode.InvalidId);
        File.Exists(outside).Should().BeTrue();
    }

    [TestMethod]
    public async Task Delete_RecordingInProgress_IsRejected()
    {
        await using var graph = new RecordingTestGraph();
        graph.EnableRecording();
        var session = await graph.OpenSessionAsync("web");
        var started = await graph.RecordingService.StartAsync(session, TestContext.CancellationToken);

        var deleted = await graph.Library.DeleteAsync(started.Value.Id.Value, TestContext.CancellationToken);

        deleted.Error!.Code.Should().Be(TerminalRecordingErrorCode.StillRecording);
        var listed = await graph.Library.ListAsync(TestContext.CancellationToken);
        listed.Should().ContainSingle(summary => summary.IsActive);
    }

    [TestMethod]
    public async Task List_ReturnsNewestFirst()
    {
        await using var graph = new RecordingTestGraph();
        var now = RecordingTestGraph.StartTime.UtcDateTime;
        var older = await graph.StoreFinishedRecordingAsync(now.AddHours(-1));
        var newer = await graph.StoreFinishedRecordingAsync(now);

        var listed = await graph.Library.ListAsync(TestContext.CancellationToken);

        listed.Select(summary => summary.Metadata.Id).Should().Equal(newer.Id, older.Id);
    }

    [TestMethod]
    public void RecordingIds_AreLowercaseHexOfFixedLength()
    {
        var id = TerminalRecordingId.Create();

        id.Value.Should().MatchRegex("^[0-9a-f]{32}$");
        TerminalRecordingId.TryParse(id.Value, out var parsed).Should().BeTrue();
        parsed.Should().Be(id);
    }
}
