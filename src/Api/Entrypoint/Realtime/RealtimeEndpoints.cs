using System.Net.WebSockets;
using DTech.Entrypoint.Realtime;

namespace Microsoft.AspNetCore.Builder;

public static class RealtimeEndpoints
{
    public const string Path = "/api/ws";

    public static IEndpointRouteBuilder MapRealtimeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.Map(Path, AcceptConnection);
        return endpoints;
    }

    private static async Task AcceptConnection(HttpContext context, RealtimeChannel channel, IHostApplicationLifetime lifetime)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var webSocket = await context.AcceptBrowserWebSocketAsync();
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, lifetime.ApplicationStopping);
        await using var connection = channel.Connect(webSocket, stopping.Token);
        await DrainUntilClosed(webSocket, connection);
    }

    private static async Task DrainUntilClosed(WebSocket webSocket, RealtimeConnection connection)
    {
        var cancellationToken = connection.Stopping;
        var buffer = new byte[256];
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    connection.RequestClose();
                    return;
                }
            }
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
        }
    }
}
