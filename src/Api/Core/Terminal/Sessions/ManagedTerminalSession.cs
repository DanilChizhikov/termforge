using DTech.Core.Connections;

namespace DTech.Core.Terminal.Sessions;

public sealed class ManagedTerminalSession
{
    private readonly ITerminalSession _session;
    private readonly CancellationTokenSource _closing = new();
    private int _attached;
    private int _closed;
    private string? _closeReason;
    private ITerminalOutputObserver? _outputObserver;
    private TerminalSize _size;

    internal ManagedTerminalSession(TerminalSessionDescriptor descriptor, string ownerKey, ITerminalSession session, TerminalSize initialSize)
    {
        Descriptor = descriptor;
        OwnerKey = ownerKey;
        _session = session;
        _size = initialSize;
    }

    public TerminalSessionDescriptor Descriptor { get; }

    public bool IsAttached => Volatile.Read(ref _attached) == 1;

    public bool IsClosed => Volatile.Read(ref _closed) == 1;

    public string? CloseReason => Volatile.Read(ref _closeReason);

    public CancellationToken Closing => _closing.Token;

    public TerminalSize Size => _size;

    public TerminalOutputTail OutputTail { get; } = new();

    internal string OwnerKey { get; }

    public bool TryAttach()
    {
        var isFirstAttachment = Interlocked.CompareExchange(ref _attached, 1, 0) == 0;
        return isFirstAttachment && !IsClosed;
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var length = await _session.ReadAsync(buffer, cancellationToken);
        if (length <= 0)
        {
            return length;
        }

        var output = buffer.Span[..length];
        OutputTail.Append(output);
        var observer = Volatile.Read(ref _outputObserver);
        observer?.Observe(output);

        return length;
    }

    public bool TryAttachOutputObserver(ITerminalOutputObserver observer)
    {
        if (IsClosed)
        {
            return false;
        }

        return Interlocked.CompareExchange(ref _outputObserver, observer, null) is null;
    }

    public void DetachOutputObserver(ITerminalOutputObserver observer)
    {
        Interlocked.CompareExchange(ref _outputObserver, null, observer);
    }

    public ValueTask<SshResult> WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        return _session.WriteAsync(data, cancellationToken);
    }

    public SshResult Resize(TerminalSize size)
    {
        var resized = _session.Resize(size);
        if (resized.IsSuccess)
        {
            _size = size;
        }

        return resized;
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
