namespace DTech.Entrypoint.Security;

internal static class RequestRejection
{
    public static async Task WriteAsync(HttpContext context, int statusCode, string reason)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "text/plain; charset=utf-8";
        try
        {
            await context.Response.WriteAsync(reason, context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
        }
    }
}
