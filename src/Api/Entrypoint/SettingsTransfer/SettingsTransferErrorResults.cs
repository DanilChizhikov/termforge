using DTech.Core.SettingsTransfer;

namespace DTech.Entrypoint.SettingsTransfer;

internal static class SettingsTransferErrorResults
{
    public const string ErrorCodeExtension = "code";

    public static IResult ToProblem(SettingsTransferError error)
    {
        var statusCode = GetStatusCode(error.Code);
        var extensions = new Dictionary<string, object?>
        {
            [ErrorCodeExtension] = error.Code.ToString(),
        };
        return Results.Problem(detail: error.Message, statusCode: statusCode, extensions: extensions);
    }

    private static int GetStatusCode(SettingsTransferErrorCode code)
    {
        return code switch
        {
            SettingsTransferErrorCode.TooLarge => StatusCodes.Status413PayloadTooLarge,
            SettingsTransferErrorCode.ConfirmationRequired => StatusCodes.Status409Conflict,
            SettingsTransferErrorCode.SecretsUnavailable => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
    }
}
