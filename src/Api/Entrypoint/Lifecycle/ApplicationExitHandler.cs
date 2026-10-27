namespace DTech.Entrypoint.Lifecycle;

internal sealed class ApplicationExitHandler(ApplicationExitCoordinator coordinator, ILogger<ApplicationExitHandler> logger)
{
    public const string ExitInProgressCode = "ExitInProgress";
    public const string NoPendingExitCode = "NoPendingExit";

    public IResult RequestExit(HttpContext context, ExitRequest? request)
    {
        var policy = request?.Policy ?? ExitTransferPolicy.CancelTransfers;
        if (!coordinator.TryBegin(policy))
        {
            return Conflict(ExitInProgressCode, "TermForge is already exiting.");
        }

        logger.LogInformation($"Application exit requested from the browser with policy {policy}");
        context.Response.OnCompleted(StartExit);
        var state = coordinator.State;
        var response = ExitStateResponse.From(state);
        return Results.Json(response, statusCode: StatusCodes.Status202Accepted);
    }

    public IResult CancelExit()
    {
        if (!coordinator.TryCancelWaiting())
        {
            return Conflict(NoPendingExitCode, "There is no pending exit that can be cancelled.");
        }

        return Results.NoContent();
    }

    private Task StartExit()
    {
        coordinator.Start();
        return Task.CompletedTask;
    }

    private static IResult Conflict(string code, string detail)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = code };
        return Results.Problem(detail: detail, statusCode: StatusCodes.Status409Conflict, extensions: extensions);
    }
}
