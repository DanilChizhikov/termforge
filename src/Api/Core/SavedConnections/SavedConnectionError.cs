using DTech.Core.Credentials;

namespace DTech.Core.SavedConnections;

public sealed record SavedConnectionError(SavedConnectionErrorCode Code, string Message)
{
    public CredentialErrorCode? CredentialCode { get; init; }

    public static SavedConnectionError Invalid(string message)
    {
        return new SavedConnectionError(SavedConnectionErrorCode.Invalid, message);
    }

    public static SavedConnectionError NotFound(Guid id)
    {
        return new SavedConnectionError(SavedConnectionErrorCode.NotFound, $"Saved connection {id} was not found.");
    }

    public static SavedConnectionError PasswordNotFound(Guid id)
    {
        return new SavedConnectionError(SavedConnectionErrorCode.NotFound, $"Saved connection {id} has no stored password.");
    }

    public static SavedConnectionError NameTaken(string name)
    {
        return new SavedConnectionError(SavedConnectionErrorCode.NameTaken, $"A saved connection named '{name}' already exists.");
    }

    public static SavedConnectionError FromCredential(CredentialError error)
    {
        return new SavedConnectionError(SavedConnectionErrorCode.CredentialStoreFailure, error.Message)
        {
            CredentialCode = error.Code,
        };
    }
}
