using DTech.Core.Files;

namespace DTech.Entrypoint.Files;

internal static class FileErrorResults
{
    public const string ErrorCodeExtension = "code";

    private const int ClientClosedRequest = 499;

    public static IResult ToProblem(FileError error)
    {
        var statusCode = GetStatusCode(error.Code);
        var extensions = new Dictionary<string, object?>
        {
            [ErrorCodeExtension] = error.Code.ToString(),
        };
        return Results.Problem(detail: error.Message, statusCode: statusCode, extensions: extensions);
    }

    private static int GetStatusCode(FileErrorCode code)
    {
        return code switch
        {
            FileErrorCode.Invalid => StatusCodes.Status400BadRequest,
            FileErrorCode.NotADirectory => StatusCodes.Status400BadRequest,
            FileErrorCode.NotFound => StatusCodes.Status404NotFound,
            FileErrorCode.PermissionDenied => StatusCodes.Status422UnprocessableEntity,
            FileErrorCode.AlreadyExists => StatusCodes.Status409Conflict,
            FileErrorCode.NotEmpty => StatusCodes.Status409Conflict,
            FileErrorCode.ConfirmationRequired => StatusCodes.Status428PreconditionRequired,
            FileErrorCode.ReadOnlyServer => StatusCodes.Status423Locked,
            FileErrorCode.ConnectionFailed => StatusCodes.Status502BadGateway,
            FileErrorCode.Cancelled => ClientClosedRequest,
            _ => StatusCodes.Status500InternalServerError,
        };
    }
}
