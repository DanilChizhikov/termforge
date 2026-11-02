using System.Net.WebSockets;
using System.Threading.Channels;

namespace DTech.Entrypoint.Realtime;

internal sealed class RealtimeConnection : IAsyncDisposable
{
    internal const int QueueCapacity = 64;
    internal static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);

    private readonly WebSocket _webSocket;
    private readonly Action<RealtimeConnection> _onClosed;
    private readonly TimeProvider _timeProvider;
    private readonly Channel<byte[]> _outgoing;
    private readonly CancellationTokenSource _stopping = new();
    private readonly TaskCompletionSource _cancellationFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenRegistration _externalCancellation;
    private Task _sender = Task.CompletedTask;
    private int _closed;
    private int _closing;
    private int _disposed;

    public RealtimeConnection(WebSocket webSocket, Action<RealtimeConnection> onClosed, TimeProvider timeProvider)
    {
        _webSocket = webSocket;
        _onClosed = onClosed;
        _timeProvider = timeProvider;
        var channelOptions = new BoundedChannelOptions(QueueCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false,
        };
        _outgoing = Channel.CreateBounded<byte[]>(channelOptions);
        Id = Guid.NewGuid();
    }

    public Guid Id { get; }

    public string? Failure { get; private set; }

    public CancellationToken Stopping => _stopping.Token;

    public void Start(CancellationToken cancellationToken)
    {
        _externalCancellation = cancellationToken.Register(Stop);
        _sender = SendLoopAsync();
    }

    public bool TryEnqueue(byte[] message)
    {
        if (Volatile.Read(ref _closed) != 0 || Volatile.Read(ref _closing) != 0)
        {
            return false;
        }

        if (_outgoing.Writer.TryWrite(message))
        {
            return true;
        }

        Stop("outgoing queue overflow");
        return false;
    }

    public void RequestClose()
    {
        Interlocked.Exchange(ref _closing, 1);
        _outgoing.Writer.TryComplete();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (Volatile.Read(ref _closing) == 0)
        {
            Stop();
        }

        try
        {
            await _sender;
            Stop();
            await _cancellationFinished.Task;
        }
        finally
        {
            _externalCancellation.Dispose();
            _stopping.Dispose();
            while (_outgoing.Reader.TryRead(out _))
            {
            }
        }
    }

    private async Task SendLoopAsync()
    {
        try
        {
            await foreach (var message in _outgoing.Reader.ReadAllAsync(Stopping))
            {
                if (Volatile.Read(ref _closed) != 0 || Volatile.Read(ref _closing) != 0 || !await SendAsync(message))
                {
                    break;
                }
            }

            if (Volatile.Read(ref _closing) != 0 && Volatile.Read(ref _closed) == 0)
            {
                var close = _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closed", Stopping);
                await WaitForSendAsync(close);
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Stop(exception.GetType().Name);
        }
        finally
        {
            Stop();
        }
    }

    private async Task<bool> SendAsync(byte[] message)
    {
        if (_webSocket.State != WebSocketState.Open)
        {
            if (_webSocket.State == WebSocketState.CloseReceived)
            {
                RequestClose();
                return false;
            }

            Stop("socket is closed");
            return false;
        }

        var memory = message.AsMemory();
        var send = _webSocket.SendAsync(memory, WebSocketMessageType.Text, true, Stopping).AsTask();
        return await WaitForSendAsync(send);
    }

    private async Task<bool> WaitForSendAsync(Task send)
    {
        try
        {
            await send.WaitAsync(SendTimeout, _timeProvider, Stopping);
            return true;
        }
        catch (TimeoutException)
        {
            Stop("send timeout");
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Stop(exception.GetType().Name);
        }

        try
        {
            await send;
        }
        catch (Exception)
        {
        }

        return false;
    }

    private void Stop()
    {
        Stop(null);
    }

    private void Stop(string? failure)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }

        Failure = failure;
        _outgoing.Writer.TryComplete();
        try
        {
            _onClosed(this);
        }
        finally
        {
            _ = FinishCancellationAsync();
        }
    }

    private async Task FinishCancellationAsync()
    {
        await Task.Yield();
        try
        {
            _webSocket.Abort();
        }
        catch (Exception exception)
        {
            Failure ??= exception.GetType().Name;
        }

        try
        {
            await _stopping.CancelAsync();
            _cancellationFinished.TrySetResult();
        }
        catch (Exception exception)
        {
            _cancellationFinished.TrySetException(exception);
        }
    }
}
