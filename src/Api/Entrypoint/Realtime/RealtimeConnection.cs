using System.Net.WebSockets;

namespace DTech.Entrypoint.Realtime;

internal sealed class RealtimeConnection : IDisposable
{
    private readonly WebSocket _webSocket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public RealtimeConnection(WebSocket webSocket)
    {
        _webSocket = webSocket;
        Id = Guid.NewGuid();
    }

    public Guid Id { get; }

    public async Task Send(ReadOnlyMemory<byte> message, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (_webSocket.State != WebSocketState.Open)
            {
                return;
            }

            await _webSocket.SendAsync(message, WebSocketMessageType.Text, true, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Dispose()
    {
        _sendLock.Dispose();
    }
}
