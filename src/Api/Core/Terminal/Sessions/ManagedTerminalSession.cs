using DTech.Core.Connections;

namespace DTech.Core.Terminal.Sessions;

public sealed class ManagedTerminalSession
{
    private readonly ITerminalSession _session;
    private readonly CancellationTokenSource _closing = new();
    private int _attached;
    private int _closed;
    private string? _closeReason;

    internal ManagedTerminalSession(TerminalSessionDescriptor descriptor, string ownerKey, ITerminalSession session)
    {
        Descriptor = descriptor;
        OwnerKey = ownerKey;
        _session = session;
    }

    public TerminalSessionDescriptor Descriptor { get; }

    public bool IsAttached => Volatile.Read(ref _attached) == 1;

    public bool IsClosed => Volatile.Read(ref _closed) == 1;

    public string? CloseReason => Volatile.Read(ref _closeReason);

    public CancellationToken Closing => _closing.Token;

    internal string OwnerKey { get; }

    public bool TryAttach()
    {
        var isFirstAttachment = Interlocked.CompareExchange(ref _attached, 1, 0) == 0;
        return isFirstAttachment && !IsClosed;
    }

    public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return _session.ReadAsync(buffer, cancellationToken);
    }

    public ValueTask<SshResult> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        return _session.WriteAsync(data, cancellationToken);
    }

    public SshResult Resize(TerminalSize size)
    {
        return _session.Resize(size);
    }

    internal async Task CloseAsync(string reason)
    {
        if (Interlocked.Exchange(ref _closed, 1) == 1)
        {
            return;
        }

        Volatile.Write(ref _closeReason, reason);
        await _closing.CancelAsync();
        await _session.DisposeAsync();
    }
}
