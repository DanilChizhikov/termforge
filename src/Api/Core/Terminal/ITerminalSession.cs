using DTech.Core.Connections;

namespace DTech.Core.Terminal;

public interface ITerminalSession : IAsyncDisposable
{
    bool IsOpen { get; }

    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken);

    ValueTask<SshResult> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken);

    SshResult Resize(TerminalSize size);
}
