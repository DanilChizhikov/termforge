using DTech.Core.Credentials;

namespace DTech.Core.SavedConnections;

public static class PasswordCredentialKey
{
    public static CredentialKey For(Guid connectionId)
    {
        var result = CredentialKey.Create($"connection:{connectionId:D}:password");
        return result.Value;
    }
}
