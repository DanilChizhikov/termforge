using System.Text;
using System.Text.Json;
using DTech.Core.Terminal.Recording;
using DTech.Core.Tests.Terminal.Recording.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Terminal.Recording;

[TestClass]
public sealed class TerminalRecorderTests
{
    private const long LargeLimit = 1024 * 1024;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Recording_IsAsciicastV2WithHeaderAndOutputEvents()
    {
        await using var graph = new RecordingTestGraph();
        var metadata = graph.CreateMetadata(RecordingTestGraph.StartTime.UtcDateTime) with { EndedAt = null, EndReason = null };
        var recorder = await StartAsync(graph, metadata, LargeLimit);

        Observe(recorder, "hello\r\n");
        Observe(recorder, "\u001b[32mgreen\u001b[0m \"quoted\"");
        await recorder.StopAsync(TerminalRecordingEndReason.Stopped);

        var lines = await ReadLinesAsync(graph, metadata.Id);
        lines.Should().HaveCount(3);
        using var header = JsonDocument.Parse(lines[0]);
        header.RootElement.GetProperty("version").GetInt32().Should().Be(2);
        header.RootElement.GetProperty("width").GetInt32().Should().Be(RecordingTestGraph.InitialSize.Columns);
        header.RootElement.GetProperty("height").GetInt32().Should().Be(RecordingTestGraph.InitialSize.Rows);
        header.RootElement.GetProperty("timestamp").GetInt64().Should().Be(RecordingTestGraph.StartTime.ToUnixTimeSeconds());
        var first = ParseEvent(lines[1]);
        var second = ParseEvent(lines[2]);
        first.Type.Should().Be("o");
        first.Data.Should().Be("hello\r\n");
        second.Data.Should().Be("\u001b[32mgreen\u001b[0m \"quoted\"");
        first.Time.Should().BeGreaterThanOrEqualTo(0);
        second.Time.Should().BeGreaterThanOrEqualTo(first.Time);
    }

    [TestMethod]
    public async Task Recording_KeepsMultibyteCharactersSplitAcrossReads()
    {
        await using var graph = new RecordingTestGraph();
        var metadata = graph.CreateMetadata(RecordingTestGraph.StartTime.UtcDateTime);
        var recorder = await StartAsync(graph, metadata, LargeLimit);
        var bytes = Encoding.UTF8.GetBytes("café ✓");

        recorder.Observe(bytes.AsSpan(0, 4));
        recorder.Observe(bytes.AsSpan(4));
        await recorder.StopAsync(TerminalRecordingEndReason.Stopped);

        var lines = await ReadLinesAsync(graph, metadata.Id);
        var text = string.Concat(lines.Skip(1).Select(line => ParseEvent(line).Data));
        text.Should().Be("café ✓");
    }

    [TestMethod]
    public async Task Recording_StopsAndIsMarkedTruncatedWhenTheSizeLimitIsReached()
    {
        await using var graph = new RecordingTestGraph();
        var metadata = graph.CreateMetadata(RecordingTestGraph.StartTime.UtcDateTime);
        var headerLength = AsciicastFormat.Header(metadata).Length;
        var limit = headerLength + 120;
        var recorder = await StartAsync(graph, metadata, limit);

        Observe(recorder, new string('a', 40));
        Observe(recorder, new string('b', 40));
        Observe(recorder, new string('c', 40));
        await recorder.Completion;
        Observe(recorder, "after the limit");

        var castLength = new FileInfo(CastPath(graph, metadata.Id)).Length;
        castLength.Should().BeLessThanOrEqualTo(limit);
        var lines = await ReadLinesAsync(graph, metadata.Id);
        lines.Should().HaveCount(3);
        var stored = await graph.Storage.FindAsync(metadata.Id, TestContext.CancellationToken);
        stored!.EndReason.Should().Be(TerminalRecordingEndReason.SizeLimitReached);
        stored.IsTruncated.Should().BeTrue();
        stored.SizeBytes.Should().Be(castLength);
        recorder.IsActive.Should().BeFalse();
    }

    [TestMethod]
    public async Task Recording_WhenTheWriterFallsBehind_StopsInsteadOfBlockingTheTerminal()
    {
        await using var graph = new RecordingTestGraph();
        var metadata = graph.CreateMetadata(RecordingTestGraph.StartTime.UtcDateTime);
        var setup = new TerminalRecorderSetup(metadata, LargeLimit) { ChannelCapacity = 1 };
        var started = await graph.RecorderFactory.StartAsync(setup, TestContext.CancellationToken);
        var recorder = started.Value;

        for (var i = 0; i < 100_000 && recorder.IsActive; i++)
        {
            Observe(recorder, "flood");
        }

        await recorder.Completion;

        recorder.Metadata.EndReason.Should().Be(TerminalRecordingEndReason.WriterFellBehind);
        recorder.Metadata.IsTruncated.Should().BeTrue();
    }

    private static async Task<TerminalRecorder> StartAsync(RecordingTestGraph graph, TerminalRecordingMetadata metadata, long maxBytes)
    {
        var setup = new TerminalRecorderSetup(metadata, maxBytes);
        var started = await graph.RecorderFactory.StartAsync(setup, CancellationToken.None);
        return started.Value;
    }

    private static void Observe(TerminalRecorder recorder, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        recorder.Observe(bytes);
    }

    private static string CastPath(RecordingTestGraph graph, TerminalRecordingId id)
    {
        return Path.Combine(graph.Storage.DirectoryPath, id.Value + AsciicastFormat.FileExtension);
    }

    private async Task<string[]> ReadLinesAsync(RecordingTestGraph graph, TerminalRecordingId id)
    {
        var path = CastPath(graph, id);
        var lines = await File.ReadAllLinesAsync(path, TestContext.CancellationToken);
        return lines.Where(line => line.Length > 0).ToArray();
    }

    private static RecordedEvent ParseEvent(string line)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        root.GetArrayLength().Should().Be(3);
        return new RecordedEvent(root[0].GetDouble(), root[1].GetString()!, root[2].GetString()!);
    }

    private sealed record RecordedEvent(double Time, string Type, string Data);
}
