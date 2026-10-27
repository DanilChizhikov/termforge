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

    private static async Task AcceptConnection(HttpContext context, RealtimeChannel channel)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        using var connection = new RealtimeConnection(webSocket);
        channel.Add(connection);
        try
        {
            await DrainUntilClosed(webSocket, context.RequestAborted);
        }
        finally
        {
            channel.Remove(connection);
        }
    }

    private static async Task DrainUntilClosed(WebSocket webSocket, CancellationToken cancellationToken)
    {
        var buffer = new byte[256];
        try
        {
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closed", CancellationToken.None);
                }
            }
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
        {
        }
    }
}
