namespace DTech.Entrypoint.Security;

internal sealed class CsrfProtectionMiddleware(RequestDelegate next, ILogger<CsrfProtectionMiddleware> logger)
{
    public const string HeaderName = "X-CSRF-Token";
    public const string WebSocketProtocolPrefix = "termforge.";

    public Task InvokeAsync(HttpContext context)
    {
        if (!RequiresCsrfToken(context) || HasValidCsrfToken(context))
        {
            return next(context);
        }

        logger.LogWarning($"Rejected {context.Request.Method} request to {context.Request.Path} without a valid CSRF token");
        return RequestRejection.WriteAsync(context, StatusCodes.Status403Forbidden, "A valid CSRF token is required");
    }

    private static bool RequiresCsrfToken(HttpContext context)
    {
        var request = context.Request;
        if (context.WebSockets.IsWebSocketRequest)
        {
            return !SecurityPaths.IsAiBridge(request.Path);
        }

        var isSafeMethod = HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method)
            || HttpMethods.IsTrace(request.Method);
        return (SecurityPaths.IsApi(request.Path) || !isSafeMethod)
            && !SecurityPaths.IsSessionEstablishment(request.Path)
            && !SecurityPaths.IsAiBridge(request.Path);
    }

    private static bool HasValidCsrfToken(HttpContext context)
    {
        var session = context.Features.Get<BrowserSession>();
        if (session is null)
        {
            return false;
        }

        if (context.WebSockets.IsWebSocketRequest)
        {
            var protocols = context.WebSockets.WebSocketRequestedProtocols;
            for (var i = 0; i < protocols.Count; i++)
            {
                var protocol = protocols[i];
                if (protocol.StartsWith(WebSocketProtocolPrefix, StringComparison.Ordinal)
                    && SecretComparer.AreEqual(session.CsrfToken, protocol[WebSocketProtocolPrefix.Length..]))
                {
                    return true;
                }
            }

            return false;
        }

        var csrfToken = context.Request.Headers[HeaderName].ToString();
        return SecretComparer.AreEqual(session.CsrfToken, csrfToken);
    }
}
