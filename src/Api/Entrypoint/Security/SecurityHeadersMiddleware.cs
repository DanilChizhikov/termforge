using Microsoft.Net.Http.Headers;

namespace DTech.Entrypoint.Security;

internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers[HeaderNames.XContentTypeOptions] = "nosniff";
        headers[HeaderNames.XFrameOptions] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        if (SecurityPaths.IsApi(context.Request.Path))
        {
            headers[HeaderNames.CacheControl] = "no-store";
        }

        return next(context);
    }
}
