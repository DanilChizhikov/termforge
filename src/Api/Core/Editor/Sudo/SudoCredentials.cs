using DTech.Core.Credentials;

namespace DTech.Core.Editor.Sudo;

public sealed class SudoCredentials(string? password, bool rememberPassword)
{
    public static SudoCredentials None { get; } = new(null, false);

    public string? Password { get; } = string.IsNullOrEmpty(password) ? null : password;

    public bool RememberPassword { get; } = rememberPassword;

    public override string ToString()
    {
        return Secret.Redacted;
    }
}
