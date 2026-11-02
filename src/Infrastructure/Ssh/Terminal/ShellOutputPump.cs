using System.Threading.Channels;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace DTech.Ssh.Terminal;

internal sealed class ShellOutputPump
{
    private const int ChunkSize = 16 * 1024;
    private const int BufferedChunkLimit = 64;

    private readonly Stream _shellStream;
    private readonly Action _stopSource;
    private readonly Channel<ReadOnlyMemory<byte>> _chunks;
    private readonly Task _pumpTask;
    private ReadOnlyMemory<byte> _pendingChunk;
    private Exception? _failure;
    private int _stopping;

    public ShellOutputPump(Stream shellStream, Action stopSource)
    {
        _shellStream = shellStream;
        _stopSource = stopSource;
        var channelOptions = new BoundedChannelOptions(BufferedChunkLimit)
        {
            SingleReader = true,
            SingleWriter = true,
        };
        _chunks = Channel.CreateBounded<ReadOnlyMemory<byte>>(channelOptions);
        _pumpTask = Task.Factory.StartNew(Pump, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    public Exception? Failure => Volatile.Read(ref _failure);

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
        Interlocked.Exchange(ref _stopping, 1);
        _chunks.Writer.TryComplete();
        _shellStream.Dispose();
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
            if (Volatile.Read(ref _stopping) == 0)
            {
                Interlocked.CompareExchange(ref _failure, exception, null);
            }
        }
        finally
        {
            _chunks.Writer.TryComplete();
        }
    }

    private int ReadChunk(out byte[] chunk)
    {
        chunk = new byte[ChunkSize];
        if (_shellStream is ShellStream or PipeStream && _shellStream.Length > ChunkSize * BufferedChunkLimit)
        {
            StopOverflowedSource();
            return 0;
        }

        return _shellStream.Read(chunk, 0, chunk.Length);
    }

    private bool Publish(ReadOnlyMemory<byte> receivedPart)
    {
        if (_chunks.Writer.TryWrite(receivedPart))
        {
            return true;
        }

        StopOverflowedSource();
        return false;
    }

    private void StopOverflowedSource()
    {
        if (Volatile.Read(ref _stopping) == 0)
        {
            var overflow = new IOException("The SSH output backlog exceeded 64 chunks of 16 KiB; the stream was closed.");
            Interlocked.CompareExchange(ref _failure, overflow, null);
            _stopSource();
        }
    }
}
