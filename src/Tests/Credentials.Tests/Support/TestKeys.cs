using DTech.Core.Credentials;

namespace DTech.Credentials.Tests.Support;

public static class TestKeys
{
    public static CredentialKey Create(string value)
    {
        var result = CredentialKey.Create(value);
        return result.Value;
    }

    public static CredentialKey CreateUnique()
    {
        var value = $"termforge-tests/{Guid.NewGuid():N}";
        return Create(value);
    }
}
