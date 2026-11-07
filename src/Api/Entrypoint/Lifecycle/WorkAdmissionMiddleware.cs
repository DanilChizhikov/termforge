using DTech.Entrypoint.Security;

namespace DTech.Entrypoint.Lifecycle;

internal sealed class WorkAdmissionMiddleware(RequestDelegate next, WorkAdmissionGate gate)
{
    public const string ExitingCode = "ApplicationExiting";

    private static readonly string[] AllowedWhileExiting =
    [
        ApplicationLifecycleExtensions.LifecyclePath,
        SecurityPaths.Session,
        WorkspaceEndpoints.Route,
        "/api/updates/install",
        "/api/updates/cancel",
    ];

    public Task InvokeAsync(HttpContext context)
    {
        if (gate.IsAcceptingWork || !StartsNewWork(context))
        {
            return next(context);
        }

        var extensions = new Dictionary<string, object?> { ["code"] = ExitingCode };
        var rejection = Results.Problem(
            detail: "TermForge is exiting and does not accept new work.",
            statusCode: StatusCodes.Status503ServiceUnavailable,
            extensions: extensions);
        return rejection.ExecuteAsync(context);
    }

    private static bool StartsNewWork(HttpContext context)
    {
        var request = context.Request;
        if (context.WebSockets.IsWebSocketRequest || SecurityPaths.IsAiBridge(request.Path))
        {
            return true;
        }

        var isSafeMethod = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method);
        if (isSafeMethod || !SecurityPaths.IsApi(request.Path))
        {
            return false;
        }

        for (var i = 0; i < AllowedWhileExiting.Length; i++)
        {
            if (request.Path.StartsWithSegments(AllowedWhileExiting[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
