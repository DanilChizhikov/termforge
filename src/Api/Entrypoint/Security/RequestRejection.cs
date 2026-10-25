namespace DTech.Entrypoint.Security;

internal static class RequestRejection
{
    public static Task WriteAsync(HttpContext context, int statusCode, string reason)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "text/plain; charset=utf-8";
        return context.Response.WriteAsync(reason, context.RequestAborted);
    }
}
