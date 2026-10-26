namespace DTech.Ssh.IntegrationTests.Transfers;

internal sealed class InterceptingStream(Stream inner, StreamInterception interception) : Stream
{
    private long _read;
    private long _written;

    public override bool CanRead => inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => inner.CanWrite;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        inner.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return inner.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await DelayAsync(cancellationToken);
        var allowed = buffer;
        if (interception.ReadLimit is { } limit)
        {
            var remaining = (int)Math.Max(0, Math.Min(buffer.Length, limit - _read));
            allowed = buffer[..remaining];
        }

        if (allowed.Length == 0)
        {
            return 0;
        }

        var read = await inner.ReadAsync(allowed, cancellationToken);
        _read += read;
        return read;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await DelayAsync(cancellationToken);
        if (interception.WriteLimit is { } limit && _written + buffer.Length > limit)
        {
            var remaining = (int)Math.Max(0, limit - _written);
            var head = buffer[..remaining];
            await inner.WriteAsync(head, cancellationToken);
            _written += remaining;
            throw new IOException("Simulated connection reset during transfer.");
        }

        await inner.WriteAsync(buffer, cancellationToken);
        _written += buffer.Length;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    private Task DelayAsync(CancellationToken cancellationToken)
    {
        return interception.Delay is { } delay ? Task.Delay(delay, cancellationToken) : Task.CompletedTask;
    }
}
