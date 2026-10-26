using DTech.Core.Diagnostics;

namespace DTech.Core.Tests.Docker.Support;

internal sealed class RecordedCommandStream(IReadOnlyList<byte[]> chunks, int exitCode) : IRemoteCommandStream
{
    private int _nextChunk;

    public bool Disposed { get; private set; }

    public int? ExitCode => _nextChunk >= chunks.Count ? exitCode : null;

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_nextChunk >= chunks.Count)
        {
            return ValueTask.FromResult(0);
        }

        var chunk = chunks[_nextChunk];
        _nextChunk++;
        chunk.CopyTo(buffer);
        return ValueTask.FromResult(chunk.Length);
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}
