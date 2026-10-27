using DTech.Core.Credentials;
using DTech.Core.Editor.Sudo;
using DTech.Core.SavedConnections;

namespace DTech.Core.SettingsTransfer.Secrets;

public static class ConnectionSecretKeys
{
    private const string Prefix = "connection:";
    private const string PasswordSuffix = ":password";
    private const string SudoPasswordSuffix = ":sudo-password";
    private const int GuidLength = 36;

    public static CredentialKey For(Guid connectionId, ArchivedSecretKind kind)
    {
        return kind == ArchivedSecretKind.Password ? PasswordCredentialKey.For(connectionId) : SudoPasswordStore.KeyFor(connectionId);
    }

    public static bool TryParse(string key, out Guid connectionId, out ArchivedSecretKind kind)
    {
        connectionId = Guid.Empty;
        kind = ArchivedSecretKind.Password;
        if (!key.StartsWith(Prefix, StringComparison.Ordinal) || key.Length <= Prefix.Length + GuidLength)
        {
            return false;
        }

        var idText = key.Substring(Prefix.Length, GuidLength);
        var suffix = key[(Prefix.Length + GuidLength)..];
        if (!Guid.TryParseExact(idText, "D", out connectionId) || connectionId == Guid.Empty)
        {
            return false;
        }

        if (string.Equals(suffix, PasswordSuffix, StringComparison.Ordinal))
        {
            kind = ArchivedSecretKind.Password;
            return true;
        }

        if (string.Equals(suffix, SudoPasswordSuffix, StringComparison.Ordinal))
        {
            kind = ArchivedSecretKind.SudoPassword;
            return true;
        }

        return false;
    }
}
