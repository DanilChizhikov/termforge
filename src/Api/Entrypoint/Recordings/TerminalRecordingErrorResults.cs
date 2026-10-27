using DTech.Core.Terminal.Recording;

namespace DTech.Entrypoint.Recordings;

internal static class TerminalRecordingErrorResults
{
    public const string ErrorCodeExtension = "code";

    public static IResult ToProblem(TerminalRecordingError error)
    {
        var statusCode = GetStatusCode(error.Code);
        var extensions = new Dictionary<string, object?>
        {
            [ErrorCodeExtension] = error.Code.ToString(),
        };
        return Results.Problem(detail: error.Message, statusCode: statusCode, extensions: extensions);
    }

    private static int GetStatusCode(TerminalRecordingErrorCode code)
    {
        return code switch
        {
            TerminalRecordingErrorCode.InvalidSettings => StatusCodes.Status400BadRequest,
            TerminalRecordingErrorCode.InvalidId => StatusCodes.Status400BadRequest,
            TerminalRecordingErrorCode.NotFound => StatusCodes.Status404NotFound,
            TerminalRecordingErrorCode.StorageFailed => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status409Conflict,
        };
    }
}
