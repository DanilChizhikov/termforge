namespace DTech.Entrypoint.Security;

internal sealed class HostHeaderValidationMiddleware(RequestDelegate next, ILogger<HostHeaderValidationMiddleware> logger)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (LoopbackHost.IsLoopback(context.Request.Host.Host))
        {
            return next(context);
        }

        logger.LogWarning($"Rejected request to {context.Request.Path} with a non-loopback Host header");
        return RequestRejection.WriteAsync(context, StatusCodes.Status400BadRequest, "Invalid Host header");
    }
}
