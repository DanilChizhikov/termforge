using DTech.Core.Terminal.Sessions;

namespace DTech.Entrypoint.Terminals;

internal static class TerminalErrorResults
{
    public const string ErrorCodeExtension = "code";
    public const string SshErrorKindExtension = "sshErrorKind";

    public static IResult ToProblem(TerminalError error)
    {
        var statusCode = GetStatusCode(error.Code);
        var extensions = new Dictionary<string, object?>
        {
            [ErrorCodeExtension] = error.Code.ToString(),
            [SshErrorKindExtension] = error.SshErrorKind?.ToString(),
        };
        return Results.Problem(detail: error.Message, statusCode: statusCode, extensions: extensions);
    }

    private static int GetStatusCode(TerminalErrorCode code)
    {
        return code switch
        {
            TerminalErrorCode.InvalidRequest => StatusCodes.Status400BadRequest,
            TerminalErrorCode.NotFound => StatusCodes.Status404NotFound,
            TerminalErrorCode.CredentialsUnavailable => StatusCodes.Status409Conflict,
            TerminalErrorCode.ReconnectDisabled => StatusCodes.Status409Conflict,
            TerminalErrorCode.SessionLimitReached => StatusCodes.Status429TooManyRequests,
            TerminalErrorCode.ShuttingDown => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status502BadGateway,
        };
    }
}
