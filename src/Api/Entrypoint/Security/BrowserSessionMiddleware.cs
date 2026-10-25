namespace DTech.Entrypoint.Security;

internal sealed class BrowserSessionMiddleware(RequestDelegate next, BrowserSessionStore sessionStore, ILogger<BrowserSessionMiddleware> logger)
{
    public Task InvokeAsync(HttpContext context)
    {
        var sessionToken = SessionCookie.Read(context.Request);
        var session = sessionStore.Find(sessionToken);
        if (session is not null)
        {
            context.Features.Set(session);
            return next(context);
        }

        if (!RequiresSession(context))
        {
            return next(context);
        }

        logger.LogWarning($"Rejected request to {context.Request.Path} without a valid browser session");
        return RequestRejection.WriteAsync(context, StatusCodes.Status401Unauthorized, "A valid browser session is required");
    }

    private static bool RequiresSession(HttpContext context)
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            return true;
        }

        var path = context.Request.Path;
        return SecurityPaths.IsApi(path) && !SecurityPaths.IsBootstrapExchange(path);
    }
}
