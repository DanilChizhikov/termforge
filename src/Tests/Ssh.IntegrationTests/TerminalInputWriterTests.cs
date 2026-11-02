using DTech.Ssh.Terminal;
using FluentAssertions;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class TerminalInputWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConcurrentWrites_KeepWriteAndFlushInOneCriticalSection()
    {
        using var stream = new PausedWriteStream();
        var writer = new TerminalInputWriter(stream);
        var first = writer.WriteAsync("first"u8.ToArray(), TestContext.CancellationToken).AsTask();
        await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        var second = writer.WriteAsync("second"u8.ToArray(), TestContext.CancellationToken).AsTask();

        stream.Calls.Should().Equal("write:first");
        stream.Resume.TrySetResult();
        await Task.WhenAll(first, second);

        stream.Calls.Should().Equal("write:first", "flush", "write:second", "flush");
    }

    [TestMethod]
    public async Task CancelledWaiter_DoesNotWriteOrPreventTheNextWriter()
    {
        using var stream = new PausedWriteStream();
        var writer = new TerminalInputWriter(stream);
        var first = writer.WriteAsync("first"u8.ToArray(), TestContext.CancellationToken).AsTask();
        await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        using var cancelled = new CancellationTokenSource();
        var waiting = writer.WriteAsync("cancelled"u8.ToArray(), cancelled.Token).AsTask();

        await cancelled.CancelAsync();
        var observe = async () => await waiting;
        await observe.Should().ThrowAsync<OperationCanceledException>();
        stream.Resume.TrySetResult();
        await first;
        await writer.WriteAsync("last"u8.ToArray(), TestContext.CancellationToken);

        stream.Calls.Should().Equal("write:first", "flush", "write:last", "flush");
    }

    private sealed class PausedWriteStream : MemoryStream
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Calls { get; } = [];

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var text = System.Text.Encoding.UTF8.GetString(buffer.Span);
            Calls.Add($"write:{text}");
            Started.TrySetResult();
            await Resume.Task.WaitAsync(cancellationToken);
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            Calls.Add("flush");
            return Task.CompletedTask;
        }
    }
}
