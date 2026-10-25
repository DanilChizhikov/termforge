using Microsoft.Net.Http.Headers;

namespace DTech.Entrypoint.Security;

internal sealed class OriginValidationMiddleware(RequestDelegate next, ILogger<OriginValidationMiddleware> logger)
{
    private const string SecFetchSiteHeader = "Sec-Fetch-Site";
    private const string SameOriginFetchSite = "same-origin";
    private const string UserInitiatedFetchSite = "none";

    public Task InvokeAsync(HttpContext context)
    {
        var rejectionReason = FindRejectionReason(context);
        if (rejectionReason is null)
        {
            return next(context);
        }

        logger.LogWarning($"Rejected request to {context.Request.Path}: {rejectionReason}");
        return RequestRejection.WriteAsync(context, StatusCodes.Status403Forbidden, rejectionReason);
    }

    private static string? FindRejectionReason(HttpContext context)
    {
        var request = context.Request;
        var origin = request.Headers[HeaderNames.Origin].ToString();
        var isWebSocketRequest = context.WebSockets.IsWebSocketRequest;
        if (string.IsNullOrEmpty(origin))
        {
            return isWebSocketRequest ? "WebSocket requests must send an Origin header" : FindFetchSiteRejectionReason(request);
        }

        var expectedOrigin = $"{request.Scheme}://{request.Host.Value}";
        if (!string.Equals(origin, expectedOrigin, StringComparison.OrdinalIgnoreCase))
        {
            return "Cross-origin requests are not allowed";
        }

        return FindFetchSiteRejectionReason(request);
    }

    private static string? FindFetchSiteRejectionReason(HttpRequest request)
    {
        var fetchSite = request.Headers[SecFetchSiteHeader].ToString();
        if (string.IsNullOrEmpty(fetchSite))
        {
            return null;
        }

        var isAllowedFetchSite = string.Equals(fetchSite, SameOriginFetchSite, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fetchSite, UserInitiatedFetchSite, StringComparison.OrdinalIgnoreCase);
        return isAllowedFetchSite ? null : "Cross-site requests are not allowed";
    }
}
