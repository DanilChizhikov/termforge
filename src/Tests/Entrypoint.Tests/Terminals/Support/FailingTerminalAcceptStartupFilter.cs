using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace DTech.Entrypoint.Tests.Terminals.Support;

internal sealed class FailingTerminalAcceptStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.Use(async (context, runNext) =>
            {
                if (context.Request.Path.StartsWithSegments("/api/terminals") && context.Request.Path.Value!.EndsWith("/ws", StringComparison.Ordinal))
                {
                    var feature = new FailingAcceptFeature();
                    context.Features.Set<IHttpWebSocketFeature>(feature);
                }

                await runNext(context);
            });
            next(app);
        };
    }

    private sealed class FailingAcceptFeature : IHttpWebSocketFeature
    {
        public bool IsWebSocketRequest => true;

        public Task<WebSocket> AcceptAsync(WebSocketAcceptContext context)
        {
            throw new IOException("The WebSocket handshake failed.");
        }
    }
}
