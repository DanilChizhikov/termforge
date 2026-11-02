using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;

namespace DTech.Entrypoint.Tests.Realtime;

internal sealed class TestRealtimeSocket(bool blockSends = false) : WebSocket
{
    private readonly Channel<string> _messages = Channel.CreateUnbounded<string>();
    private readonly TaskCompletionSource _sendRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _sendStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _abortStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile WebSocketState _state = WebSocketState.Open;
    private int _activeSends;
    private int _abortCount;
    private int _closeCount;

    public Task SendStarted => _sendStarted.Task;
    public Task AbortStarted => _abortStarted.Task;
    public ManualResetEventSlim? AbortRelease { get; init; }
    public int AbortCount => Volatile.Read(ref _abortCount);
    public int CloseCount => Volatile.Read(ref _closeCount);
    public bool ConcurrentSends { get; private set; }
    public Exception? SendFailure { get; init; }
    public override WebSocketCloseStatus? CloseStatus => null;
    public override string? CloseStatusDescription => null;
    public override WebSocketState State => _state;
    public override string? SubProtocol => null;

    public ValueTask<string> ReadMessageAsync(CancellationToken cancellationToken)
    {
        return _messages.Reader.ReadAsync(cancellationToken);
    }

    public void ReleaseSend()
    {
        _sendRelease.TrySetResult();
    }

    public override void Abort()
    {
        Interlocked.Increment(ref _abortCount);
        _abortStarted.TrySetResult();
        AbortRelease?.Wait();
        _state = WebSocketState.Aborted;
        _sendRelease.TrySetCanceled();
    }

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        return CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
    }

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        EnterSend();
        Interlocked.Increment(ref _closeCount);
        _state = WebSocketState.Closed;
        Interlocked.Decrement(ref _activeSends);
        return Task.CompletedTask;
    }

    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        EnterSend();
        _sendStarted.TrySetResult();
        try
        {
            if (SendFailure is not null)
            {
                throw SendFailure;
            }

            if (blockSends)
            {
                await _sendRelease.Task;
            }

            var message = Encoding.UTF8.GetString(buffer.AsSpan());
            _messages.Writer.TryWrite(message);
        }
        finally
        {
            Interlocked.Decrement(ref _activeSends);
        }
    }

    public override void Dispose()
    {
        _state = WebSocketState.Closed;
        _sendRelease.TrySetCanceled();
    }

    private void EnterSend()
    {
        if (Interlocked.Increment(ref _activeSends) != 1)
        {
            ConcurrentSends = true;
        }
    }
}
