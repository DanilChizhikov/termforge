using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using DTech.Core.Docker;

namespace DTech.Entrypoint.Docker;

internal sealed class ContainerLogRelay(ILogger<ContainerLogRelay> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly TimeSpan CloseHandshakeTimeout = TimeSpan.FromSeconds(2);

    public async Task RunAsync(WebSocket webSocket, ContainerLogStream stream, CancellationToken requestAborted)
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
        var clientClosed = WaitForClientCloseAsync(webSocket, stopping);
        try
        {
            await RelayOutputAsync(webSocket, stream, stopping.Token);
            var end = ContainerLogMessage.End(stream.ExitCode);
            await SendAsync(webSocket, end, stopping.Token);
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException or ObjectDisposedException)
        {
            logger.LogDebug($"Container log stream stopped: {exception.GetType().Name}");
        }
        finally
        {
            await stream.DisposeAsync();
        }

        stopping.CancelAfter(CloseHandshakeTimeout);
        await TryCloseAsync(webSocket, stopping.Token);
        await stopping.CancelAsync();
        await clientClosed;
    }

    public async Task RejectAsync(WebSocket webSocket, DockerError error, CancellationToken cancellationToken)
    {
        var message = ContainerLogMessage.Error(error.Message);
        try
        {
            await SendAsync(webSocket, message, cancellationToken);
            await TryCloseAsync(webSocket, cancellationToken);
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException or ObjectDisposedException)
        {
            logger.LogDebug($"Container log rejection was not delivered: {exception.GetType().Name}");
        }
    }

    private static async Task RelayOutputAsync(WebSocket webSocket, ContainerLogStream stream, CancellationToken cancellationToken)
    {
        var text = await stream.ReadAsync(cancellationToken);
        while (text is not null)
        {
            var output = ContainerLogMessage.Output(text);
            await SendAsync(webSocket, output, cancellationToken);
            text = await stream.ReadAsync(cancellationToken);
        }
    }

    private static async Task SendAsync(WebSocket webSocket, ContainerLogMessage message, CancellationToken cancellationToken)
    {
        if (webSocket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            throw new OperationCanceledException("The log viewer disconnected.");
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        await webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    private static async Task WaitForClientCloseAsync(WebSocket webSocket, CancellationTokenSource stopping)
    {
        var buffer = new byte[256];
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var received = await webSocket.ReceiveAsync(buffer, stopping.Token);
                if (received.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException or ObjectDisposedException)
        {
            return;
        }

        await stopping.CancelAsync();
    }

    private static async Task TryCloseAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        try
        {
            if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "log stream closed", cancellationToken);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException or ObjectDisposedException)
        {
        }
    }
}
