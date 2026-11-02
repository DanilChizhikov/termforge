namespace DTech.Ssh.Terminal;

internal sealed class TerminalInputWriter(Stream stream)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await stream.WriteAsync(data, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}
