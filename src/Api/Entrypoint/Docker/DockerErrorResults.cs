using DTech.Core.Docker;

namespace DTech.Entrypoint.Docker;

internal static class DockerErrorResults
{
    public const string ErrorCodeExtension = "code";
    public const string SshErrorKindExtension = "sshErrorKind";

    public static IResult ToProblem(DockerError error)
    {
        var statusCode = GetStatusCode(error.Code);
        var extensions = new Dictionary<string, object?>
        {
            [ErrorCodeExtension] = error.Code.ToString(),
            [SshErrorKindExtension] = error.SshErrorKind?.ToString(),
        };
        return Results.Problem(detail: error.Message, statusCode: statusCode, extensions: extensions);
    }

    private static int GetStatusCode(DockerErrorCode code)
    {
        return code switch
        {
            DockerErrorCode.InvalidRequest => StatusCodes.Status400BadRequest,
            DockerErrorCode.ConnectionNotFound => StatusCodes.Status404NotFound,
            DockerErrorCode.ContainerNotFound => StatusCodes.Status404NotFound,
            DockerErrorCode.CredentialsUnavailable => StatusCodes.Status409Conflict,
            DockerErrorCode.ConfirmationRequired => StatusCodes.Status409Conflict,
            DockerErrorCode.DockerMissing => StatusCodes.Status409Conflict,
            DockerErrorCode.PermissionDenied => StatusCodes.Status409Conflict,
            DockerErrorCode.DaemonUnavailable => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status502BadGateway,
        };
    }
}
