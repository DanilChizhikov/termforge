using System.Net.WebSockets;
using Microsoft.AspNetCore.Http.Features;

namespace Microsoft.AspNetCore.Http;

internal static class BrowserWebSocket
{
    public static Task<WebSocket> AcceptBrowserWebSocketAsync(this HttpContext context)
    {
        var session = context.Features.GetRequiredFeature<DTech.Entrypoint.Security.BrowserSession>();
        var protocol = DTech.Entrypoint.Security.CsrfProtectionMiddleware.WebSocketProtocolPrefix + session.CsrfToken;
        return context.WebSockets.AcceptWebSocketAsync(protocol);
    }
}
