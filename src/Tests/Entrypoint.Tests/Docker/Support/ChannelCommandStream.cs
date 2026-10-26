using System.Text;
using System.Threading.Channels;
using DTech.Core.Diagnostics;

namespace DTech.Entrypoint.Tests.Docker.Support;

internal sealed class ChannelCommandStream : IRemoteCommandStream
{
    private readonly Channel<byte[]> _chunks = Channel.CreateUnbounded<byte[]>();
    private readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int? ExitCode { get; private set; }

    public Task Disposed => _disposed.Task;

    public void Emit(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        _chunks.Writer.TryWrite(bytes);
    }

    public void Complete(int exitCode)
    {
        ExitCode = exitCode;
        _chunks.Writer.TryComplete();
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var hasData = await _chunks.Reader.WaitToReadAsync(cancellationToken);
        if (!hasData || !_chunks.Reader.TryRead(out var chunk))
        {
            return 0;
        }

        chunk.CopyTo(buffer);
        return chunk.Length;
    }

    public ValueTask DisposeAsync()
    {
        _chunks.Writer.TryComplete();
        _disposed.TrySetResult();
        return ValueTask.CompletedTask;
    }
}
