using Microsoft.Extensions.FileProviders;

namespace DTech.Entrypoint.SpaHosting;

internal sealed class SpaFallbackMiddleware
{
    private static readonly PathString ApiPrefix = new("/api");
    private static readonly PathString IndexPath = new("/index.html");

    private readonly RequestDelegate _next;
    private readonly IFileProvider _webRoot;

    public SpaFallbackMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _webRoot = environment.WebRootFileProvider;
    }

    public Task InvokeAsync(HttpContext context)
    {
        if (ShouldServeIndex(context))
        {
            context.Request.Path = IndexPath;
        }

        return _next(context);
    }

    private bool ShouldServeIndex(HttpContext context)
    {
        var isReadRequest = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
        if (!isReadRequest)
        {
            return false;
        }

        if (context.GetEndpoint() is not null)
        {
            return false;
        }

        if (context.Request.Path.StartsWithSegments(ApiPrefix))
        {
            return false;
        }

        var file = _webRoot.GetFileInfo(context.Request.Path.Value ?? string.Empty);
        return !file.Exists;
    }
}
