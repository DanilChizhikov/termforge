using DTech.Core.Credentials;

namespace DTech.Entrypoint.Credentials;

internal static class CredentialErrorResults
{
    public const string ErrorCodeExtension = "code";

    public static IResult ToProblem(CredentialError error)
    {
        var statusCode = GetStatusCode(error.Code);
        var extensions = new Dictionary<string, object?>
        {
            [ErrorCodeExtension] = error.Code.ToString(),
        };
        return Results.Problem(detail: error.Message, statusCode: statusCode, extensions: extensions);
    }

    private static int GetStatusCode(CredentialErrorCode code)
    {
        return code switch
        {
            CredentialErrorCode.InvalidKey => StatusCodes.Status400BadRequest,
            CredentialErrorCode.InvalidSecret => StatusCodes.Status400BadRequest,
            CredentialErrorCode.InvalidMasterPassword => StatusCodes.Status400BadRequest,
            CredentialErrorCode.WeakMasterPassword => StatusCodes.Status400BadRequest,
            CredentialErrorCode.NotFound => StatusCodes.Status404NotFound,
            CredentialErrorCode.VaultLocked => StatusCodes.Status423Locked,
            CredentialErrorCode.VaultNotInUse => StatusCodes.Status409Conflict,
            CredentialErrorCode.VaultCorrupted => StatusCodes.Status409Conflict,
            CredentialErrorCode.UnsupportedVaultVersion => StatusCodes.Status409Conflict,
            CredentialErrorCode.StoreUnavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError,
        };
    }
}
