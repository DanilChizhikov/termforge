using Microsoft.Net.Http.Headers;

namespace DTech.Entrypoint.Security;

internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string ContentSecurityPolicy = "default-src 'self'; script-src 'self'; connect-src 'self'; worker-src 'self'; style-src 'self' 'unsafe-inline'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";

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
        else
        {
            headers[HeaderNames.ContentSecurityPolicy] = ContentSecurityPolicy;
        }

        return next(context);
    }
}
