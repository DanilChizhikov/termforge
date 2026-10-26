namespace DTech.Core.Diagnostics;

public interface IRemoteCommandStream : IAsyncDisposable
{
    int? ExitCode { get; }

    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);
}
