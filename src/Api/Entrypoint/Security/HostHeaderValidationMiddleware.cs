using DTech.Entrypoint.Security.Network;

namespace DTech.Entrypoint.Security;

internal sealed class HostHeaderValidationMiddleware(RequestDelegate next, AllowedHostPolicy allowedHostPolicy, ILogger<HostHeaderValidationMiddleware> logger)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (allowedHostPolicy.IsAllowed(context.Request.Host.Host))
        {
            return next(context);
        }

        logger.LogWarning($"Rejected request to {context.Request.Path} with a Host header that is not loopback or an allowed network host");
        return RequestRejection.WriteAsync(context, StatusCodes.Status400BadRequest, "Invalid Host header");
    }
}
