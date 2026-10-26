using DTech.Core.Credentials;
using DTech.Core.SavedConnections;
using DTech.Entrypoint.Credentials;

namespace DTech.Entrypoint.SavedConnections;

internal static class SavedConnectionErrorResults
{
    public const string ErrorCodeExtension = "code";

    public static IResult ToProblem(SavedConnectionError error)
    {
        if (error.CredentialCode is not null)
        {
            var credentialError = new CredentialError(error.CredentialCode.Value, error.Message);
            return CredentialErrorResults.ToProblem(credentialError);
        }

        var statusCode = GetStatusCode(error.Code);
        var extensions = new Dictionary<string, object?>
        {
            [ErrorCodeExtension] = error.Code.ToString(),
        };
        return Results.Problem(detail: error.Message, statusCode: statusCode, extensions: extensions);
    }

    private static int GetStatusCode(SavedConnectionErrorCode code)
    {
        return code switch
        {
            SavedConnectionErrorCode.Invalid => StatusCodes.Status400BadRequest,
            SavedConnectionErrorCode.NotFound => StatusCodes.Status404NotFound,
            SavedConnectionErrorCode.SshConfigNotFound => StatusCodes.Status404NotFound,
            SavedConnectionErrorCode.NameTaken => StatusCodes.Status409Conflict,
            SavedConnectionErrorCode.InUse => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };
    }
}
