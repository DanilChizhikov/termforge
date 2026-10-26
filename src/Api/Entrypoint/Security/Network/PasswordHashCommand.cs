namespace DTech.Entrypoint.Security.Network;

internal static class PasswordHashCommand
{
    public const string Argument = "--hash-password";
    public const int MinimumPasswordLength = 12;

    private const int SuccessExitCode = 0;
    private const int FailureExitCode = 1;

    public static bool TryRun(string[] args)
    {
        if (Array.IndexOf(args, Argument) < 0)
        {
            return false;
        }

        Environment.ExitCode = Console.IsInputRedirected
            ? Run(Console.In, Console.Out, Console.Error)
            : RunInteractive(Console.Out, Console.Error);
        return true;
    }

    internal static int Run(TextReader input, TextWriter output, TextWriter error)
    {
        var password = input.ReadLine();
        return WriteHash(password, output, error);
    }

    private static int RunInteractive(TextWriter output, TextWriter error)
    {
        var password = ConsolePasswordPrompt.Read("Password: ");
        var confirmation = ConsolePasswordPrompt.Read("Repeat password: ");
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            error.WriteLine("The passwords do not match");
            return FailureExitCode;
        }

        return WriteHash(password, output, error);
    }

    private static int WriteHash(string? password, TextWriter output, TextWriter error)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumPasswordLength)
        {
            error.WriteLine($"The password must be at least {MinimumPasswordLength} characters long");
            return FailureExitCode;
        }

        var passwordHash = PasswordHash.Create(password, PasswordHash.DefaultIterations);
        output.WriteLine(passwordHash);
        return SuccessExitCode;
    }
}
