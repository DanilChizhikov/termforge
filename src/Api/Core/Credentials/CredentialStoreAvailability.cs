namespace DTech.Core.Credentials;

public sealed record CredentialStoreAvailability(string StoreName, bool IsAvailable, string Diagnostic)
{
    public static CredentialStoreAvailability Available(string storeName)
    {
        return new CredentialStoreAvailability(storeName, true, $"{storeName} is available.");
    }

    public static CredentialStoreAvailability Unavailable(string storeName, string diagnostic)
    {
        return new CredentialStoreAvailability(storeName, false, diagnostic);
    }
}
