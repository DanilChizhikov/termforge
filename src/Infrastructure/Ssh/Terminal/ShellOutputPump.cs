using System.Threading.Channels;
using Renci.SshNet.Common;

namespace DTech.Ssh.Terminal;

internal sealed class ShellOutputPump
{
    private const int ChunkSize = 16 * 1024;
    private const int BufferedChunkLimit = 64;

    private readonly Stream _shellStream;
    private readonly Channel<ReadOnlyMemory<byte>> _chunks;
    private readonly Task _pumpTask;
    private ReadOnlyMemory<byte> _pendingChunk;

    public ShellOutputPump(Stream shellStream)
    {
        _shellStream = shellStream;
        var channelOptions = new BoundedChannelOptions(BufferedChunkLimit)
        {
            SingleReader = true,
            SingleWriter = true,
        };
        _chunks = Channel.CreateBounded<ReadOnlyMemory<byte>>(channelOptions);
        _pumpTask = Task.Factory.StartNew(Pump, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public Exception? Failure { get; private set; }

    public bool IsCompleted => _pumpTask.IsCompleted;

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        if (_pendingChunk.IsEmpty)
        {
            var hasData = await _chunks.Reader.WaitToReadAsync(cancellationToken);
            if (!hasData || !_chunks.Reader.TryRead(out _pendingChunk))
            {
                return 0;
            }
        }

        var copiedLength = Math.Min(buffer.Length, _pendingChunk.Length);
        var copiedPart = _pendingChunk[..copiedLength];
        copiedPart.Span.CopyTo(buffer.Span);
        _pendingChunk = _pendingChunk[copiedLength..];
        return copiedLength;
    }

    public Task StopAsync()
    {
        _chunks.Writer.TryComplete();
        return _pumpTask;
    }

    private void Pump()
    {
        try
        {
            var readLength = ReadChunk(out var chunk);
            while (readLength > 0)
            {
                var receivedPart = chunk.AsMemory(0, readLength);
                var isAccepted = Publish(receivedPart);
                if (!isAccepted)
                {
                    return;
                }

                readLength = ReadChunk(out chunk);
            }
        }
        catch (Exception exception) when (exception is ObjectDisposedException or IOException or SshException)
        {
            Failure = exception;
        }
        finally
        {
            _chunks.Writer.TryComplete();
        }
    }

    private int ReadChunk(out byte[] chunk)
    {
        chunk = new byte[ChunkSize];
        return _shellStream.Read(chunk, 0, chunk.Length);
    }

    private bool Publish(ReadOnlyMemory<byte> receivedPart)
    {
        while (!_chunks.Writer.TryWrite(receivedPart))
        {
            var canWrite = _chunks.Writer.WaitToWriteAsync().AsTask().GetAwaiter().GetResult();
            if (!canWrite)
            {
                return false;
            }
        }

        return true;
    }
}
