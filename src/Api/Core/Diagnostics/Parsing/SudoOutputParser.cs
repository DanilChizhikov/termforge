namespace DTech.Core.Diagnostics.Parsing;

public static class SudoOutputParser
{
    public const string Script = "sudo -n true 2>&1";

    public static SudoAccess Parse(RemoteCommandResult result)
    {
        if (result.Succeeded)
        {
            return SudoAccess.Passwordless;
        }

        var text = result.CombinedOutput.ToLowerInvariant();
        var notPermitted = text.Contains("not in the sudoers", StringComparison.Ordinal)
            || text.Contains("may not run sudo", StringComparison.Ordinal)
            || text.Contains("is not allowed to execute", StringComparison.Ordinal)
            || text.Contains("not allowed to run sudo", StringComparison.Ordinal);
        if (notPermitted)
        {
            return SudoAccess.NotPermitted;
        }

        var passwordRequired = text.Contains("password is required", StringComparison.Ordinal)
            || text.Contains("terminal is required", StringComparison.Ordinal);
        return passwordRequired ? SudoAccess.PasswordRequired : SudoAccess.Unknown;
    }
}
