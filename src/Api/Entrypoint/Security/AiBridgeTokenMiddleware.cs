using System.Net;
using DTech.Core.AiBridge.Settings;
using Microsoft.Net.Http.Headers;

namespace DTech.Entrypoint.Security;

internal sealed class AiBridgeTokenMiddleware(RequestDelegate next, ILogger<AiBridgeTokenMiddleware> logger)
{
    private const string BearerPrefix = "Bearer ";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!SecurityPaths.IsAiBridge(context.Request.Path))
        {
            await next(context);
            return;
        }

        var remoteAddress = context.Connection.RemoteIpAddress;
        if (remoteAddress is not null && !IPAddress.IsLoopback(remoteAddress))
        {
            logger.LogWarning($"Rejected AI bridge request from {remoteAddress}: only local clients are allowed");
            await RequestRejection.WriteAsync(context, StatusCodes.Status403Forbidden, "The AI bridge accepts local connections only");
            return;
        }

        var settingsService = context.RequestServices.GetRequiredService<AiBridgeSettingsService>();
        var settings = await settingsService.GetAsync(context.RequestAborted);
        if (!settings.Enabled)
        {
            await RequestRejection.WriteAsync(context, StatusCodes.Status404NotFound, "The AI bridge is turned off");
            return;
        }

        var token = ReadBearerToken(context.Request);
        if (!AiBridgeTokenHasher.Matches(settings.TokenHash, token))
        {
            logger.LogWarning("Rejected AI bridge request without a valid token");
            context.Response.Headers[HeaderNames.WWWAuthenticate] = "Bearer";
            await RequestRejection.WriteAsync(context, StatusCodes.Status401Unauthorized, "A valid AI bridge token is required");
            return;
        }

        await next(context);
    }

    private static string? ReadBearerToken(HttpRequest request)
    {
        var authorization = request.Headers[HeaderNames.Authorization].ToString();
        if (!authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return authorization[BearerPrefix.Length..].Trim();
    }
}
