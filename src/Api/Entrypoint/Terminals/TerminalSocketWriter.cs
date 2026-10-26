using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DTech.Entrypoint.Terminals;

internal sealed class TerminalSocketWriter(WebSocket webSocket) : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public Task SendOutputAsync(ReadOnlyMemory<byte> output, CancellationToken cancellationToken)
    {
        return SendAsync(output, WebSocketMessageType.Binary, cancellationToken);
    }

    public Task SendMessageAsync(TerminalServerMessage message, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        return SendAsync(bytes, WebSocketMessageType.Text, cancellationToken);
    }

    public async Task CloseAsync(WebSocketCloseStatus status, string description, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await webSocket.CloseOutputAsync(status, description, cancellationToken);
            }
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

    private async Task SendAsync(ReadOnlyMemory<byte> payload, WebSocketMessageType messageType, CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await webSocket.SendAsync(payload, messageType, true, cancellationToken);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }
}
