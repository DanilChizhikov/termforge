namespace DTech.Entrypoint.Security;

internal sealed class CsrfProtectionMiddleware(RequestDelegate next, ILogger<CsrfProtectionMiddleware> logger)
{
    public const string HeaderName = "X-CSRF-Token";

    public Task InvokeAsync(HttpContext context)
    {
        if (!RequiresCsrfToken(context.Request) || HasValidCsrfToken(context))
        {
            return next(context);
        }

        logger.LogWarning($"Rejected {context.Request.Method} request to {context.Request.Path} without a valid CSRF token");
        return RequestRejection.WriteAsync(context, StatusCodes.Status403Forbidden, "A valid CSRF token is required");
    }

    private static bool RequiresCsrfToken(HttpRequest request)
    {
        var isSafeMethod = HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method)
            || HttpMethods.IsTrace(request.Method);
        return !isSafeMethod && !SecurityPaths.IsBootstrapExchange(request.Path);
    }

    private static bool HasValidCsrfToken(HttpContext context)
    {
        var session = context.Features.Get<BrowserSession>();
        if (session is null)
        {
            return false;
        }

        var csrfToken = context.Request.Headers[HeaderName].ToString();
        return SecretComparer.AreEqual(session.CsrfToken, csrfToken);
    }
}
