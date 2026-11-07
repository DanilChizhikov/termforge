using System.Threading.Channels;

namespace DTech.Core.Transfers.Execution;

internal sealed class TransferRelayStream : Stream
{
    private const int ChunkSize = 64 * 1024;
    private readonly Channel<byte[]> _chunks = Channel.CreateBounded<byte[]>(4);
    private byte[]? _current;
    private int _offset;

    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public void Complete(Exception? error = null) => _chunks.Writer.TryComplete(error);

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        while (!buffer.IsEmpty)
        {
            var count = Math.Min(buffer.Length, ChunkSize);
            var chunk = buffer[..count].ToArray();
            await _chunks.Writer.WriteAsync(chunk, cancellationToken);
            buffer = buffer[count..];
        }
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (buffer.IsEmpty) return 0;
        if (_current is null || _offset == _current.Length)
        {
            if (!await _chunks.Reader.WaitToReadAsync(cancellationToken)) return 0;
            _current = await _chunks.Reader.ReadAsync(cancellationToken);
            _offset = 0;
        }

        var count = Math.Min(buffer.Length, _current.Length - _offset);
        _current.AsMemory(_offset, count).CopyTo(buffer);
        _offset += count;
        return count;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var memory = buffer.AsMemory(offset, count);
        return WriteAsync(memory, cancellationToken).AsTask();
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var memory = buffer.AsMemory(offset, count);
        return ReadAsync(memory, cancellationToken).AsTask();
    }

    public override void Flush() { }
    public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
