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
    ];

    public Task InvokeAsync(HttpContext context)
    {
        if (gate.IsAcceptingWork || !StartsNewWork(context.Request))
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

    private static bool StartsNewWork(HttpRequest request)
    {
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
