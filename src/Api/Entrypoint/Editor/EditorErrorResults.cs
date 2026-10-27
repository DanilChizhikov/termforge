using DTech.Core.Editor;

namespace DTech.Entrypoint.Editor;

internal static class EditorErrorResults
{
    public const string ErrorCodeExtension = "code";

    private const int ClientClosedRequest = 499;

    public static IResult ToProblem(EditorError error)
    {
        var statusCode = GetStatusCode(error.Code);
        var extensions = new Dictionary<string, object?>
        {
            [ErrorCodeExtension] = error.Code.ToString(),
        };
        return Results.Problem(detail: error.Message, statusCode: statusCode, extensions: extensions);
    }

    private static int GetStatusCode(EditorErrorCode code)
    {
        return code switch
        {
            EditorErrorCode.Invalid => StatusCodes.Status400BadRequest,
            EditorErrorCode.NotAFile => StatusCodes.Status400BadRequest,
            EditorErrorCode.NotFound => StatusCodes.Status404NotFound,
            EditorErrorCode.PermissionDenied => StatusCodes.Status422UnprocessableEntity,
            EditorErrorCode.TooLarge => StatusCodes.Status413PayloadTooLarge,
            EditorErrorCode.Binary => StatusCodes.Status415UnsupportedMediaType,
            EditorErrorCode.ConnectionFailed => StatusCodes.Status502BadGateway,
            EditorErrorCode.Cancelled => ClientClosedRequest,
            EditorErrorCode.SudoPasswordRequired => StatusCodes.Status422UnprocessableEntity,
            EditorErrorCode.SudoPasswordIncorrect => StatusCodes.Status422UnprocessableEntity,
            EditorErrorCode.SudoUnavailable => StatusCodes.Status422UnprocessableEntity,
            EditorErrorCode.ReadOnlyServer => StatusCodes.Status423Locked,
            EditorErrorCode.ConfirmationRequired => StatusCodes.Status428PreconditionRequired,
            _ => StatusCodes.Status500InternalServerError,
        };
    }
}
