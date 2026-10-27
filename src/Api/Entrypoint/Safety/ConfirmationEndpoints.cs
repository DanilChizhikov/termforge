using DTech.Core.Safety;
using DTech.Entrypoint.Safety;

namespace Microsoft.AspNetCore.Builder;

public static class ConfirmationEndpoints
{
    public const string RoutePrefix = "/api/confirmations";

    private const string ErrorCodeExtension = "code";

    public static IEndpointRouteBuilder MapConfirmationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(RoutePrefix, Issue);
        return endpoints;
    }

    private static async Task<IResult> Issue(ConfirmationBody body, ConfirmationIssuer issuer, CancellationToken cancellationToken)
    {
        var isNumeric = int.TryParse(body.Operation, out _);
        var isKnown = Enum.TryParse<DangerousOperation>(body.Operation, true, out var operation) && Enum.IsDefined(operation);
        if (!isKnown || isNumeric || body.ConnectionId is not { } connectionId || string.IsNullOrEmpty(body.Target))
        {
            return Problem("Invalid", "operation (a known dangerous operation), connectionId and target are required.", StatusCodes.Status400BadRequest);
        }

        var scope = new ConfirmationScope(operation, connectionId, body.Target);
        var issued = await issuer.IssueAsync(scope, cancellationToken);
        if (issued.Status == ConfirmationIssueStatus.ConnectionNotFound)
        {
            return Problem("ConnectionNotFound", $"Saved connection {connectionId} was not found.", StatusCodes.Status404NotFound);
        }

        if (issued.Status == ConfirmationIssueStatus.ReadOnlyServer)
        {
            return Problem("ReadOnlyServer", ServerWriteGate.ReadOnlyMessage, StatusCodes.Status423Locked);
        }

        var grant = issued.Grant!;
        var response = new ConfirmationResponse(grant.Token, grant.ExpiresAt, operation.ToString(), issued.ServerName!, body.Target);
        return Results.Ok(response);
    }

    private static IResult Problem(string code, string detail, int statusCode)
    {
        var extensions = new Dictionary<string, object?>
        {
            [ErrorCodeExtension] = code,
        };
        return Results.Problem(detail: detail, statusCode: statusCode, extensions: extensions);
    }
}
