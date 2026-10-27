using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace DTech.Entrypoint.Tests.Infrastructure;

internal sealed class WebSocketProbeStartupFilter : IStartupFilter
{
    public const string Path = "/ws-probe";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            next(app);
            app.Map(Path, ConfigureProbe);
        };
    }

    private static void ConfigureProbe(IApplicationBuilder probe)
    {
        probe.Run(AcceptAndCloseAsync);
    }

    private static async Task AcceptAndCloseAsync(HttpContext context)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "probe", context.RequestAborted);
    }
}
