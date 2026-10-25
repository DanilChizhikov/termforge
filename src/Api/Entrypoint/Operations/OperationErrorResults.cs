using DTech.Core.Operations;

namespace DTech.Entrypoint.Operations;

internal static class OperationErrorResults
{
    public const string ErrorCodeExtension = "code";

    public static IResult ToProblem(OperationException exception)
    {
        var statusCode = GetStatusCode(exception.Failure);
        var extensions = new Dictionary<string, object?>
        {
            [ErrorCodeExtension] = exception.Failure.ToString(),
        };
        return Results.Problem(detail: exception.Message, statusCode: statusCode, extensions: extensions);
    }

    public static IResult InvalidStatus(string status)
    {
        var extensions = new Dictionary<string, object?>
        {
            [ErrorCodeExtension] = OperationFailure.InvalidRequest.ToString(),
        };
        return Results.Problem(detail: $"unknown operation status '{status}'.", statusCode: StatusCodes.Status400BadRequest, extensions: extensions);
    }

    private static int GetStatusCode(OperationFailure failure)
    {
        return failure switch
        {
            OperationFailure.NotFound => StatusCodes.Status404NotFound,
            OperationFailure.InvalidRequest => StatusCodes.Status400BadRequest,
            OperationFailure.InvalidTransition => StatusCodes.Status409Conflict,
            OperationFailure.NotRetryable => StatusCodes.Status409Conflict,
            OperationFailure.NoRetryHandler => StatusCodes.Status501NotImplemented,
            _ => StatusCodes.Status500InternalServerError,
        };
    }
}
