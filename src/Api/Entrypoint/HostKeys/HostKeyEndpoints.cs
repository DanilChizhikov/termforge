using DTech.Entrypoint.HostKeys;
using DTech.HostKeys.Decisions;

namespace Microsoft.AspNetCore.Builder;

public static class HostKeyEndpoints
{
    public const string RoutePrefix = "/api/host-keys";
    public const string ErrorCodeExtension = "code";

    private static readonly Dictionary<string, HostKeyUserDecision> Decisions = new(StringComparer.Ordinal)
    {
        ["acceptOnce"] = HostKeyUserDecision.AcceptOnce,
        ["acceptAndSave"] = HostKeyUserDecision.AcceptAndSave,
        ["reject"] = HostKeyUserDecision.Reject,
        ["replace"] = HostKeyUserDecision.Replace,
    };

    public static IEndpointRouteBuilder MapHostKeyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix);
        group.MapGet("/pending", GetPending);
        group.MapPost("/pending/{id:guid}/decision", Decide);
        return endpoints;
    }

    private static IResult GetPending(HostKeyDecisionBroker broker)
    {
        var pendingDecisions = broker.GetPending();
        var responses = PendingHostKeyResponseMapper.Map(pendingDecisions);
        return Results.Ok(responses);
    }

    private static async Task<IResult> Decide([AsParameters] HostKeyDecisionParameters parameters, CancellationToken cancellationToken)
    {
        var request = parameters.Request;
        if (request.Decision is null || !Decisions.TryGetValue(request.Decision, out var decision))
        {
            return Results.Problem(detail: "Decision must be one of acceptOnce, acceptAndSave, reject, replace.", statusCode: StatusCodes.Status400BadRequest);
        }

        var command = new HostKeyDecisionCommand(parameters.Id, decision, request.ConfirmReplace);
        var result = await parameters.Handler.DecideAsync(command, cancellationToken);
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        var errorCode = result.ErrorCode!.Value;
        var statusCode = GetStatusCode(errorCode);
        var extensions = new Dictionary<string, object?>
        {
            [ErrorCodeExtension] = errorCode.ToString(),
        };
        return Results.Problem(detail: result.ErrorMessage, statusCode: statusCode, extensions: extensions);
    }

    private static int GetStatusCode(HostKeyDecisionErrorCode errorCode)
    {
        return errorCode switch
        {
            HostKeyDecisionErrorCode.NotFound => StatusCodes.Status404NotFound,
            HostKeyDecisionErrorCode.NotAllowed => StatusCodes.Status409Conflict,
            HostKeyDecisionErrorCode.ConfirmationRequired => StatusCodes.Status428PreconditionRequired,
            _ => StatusCodes.Status500InternalServerError,
        };
    }
}
