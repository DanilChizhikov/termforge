namespace DTech.Credentials.Native.Processes;

public static class CredentialHelperCommand
{
    public const string Argument = "--credential-helper";

    public static async Task<bool> TryRunAsync(string[] args)
    {
        if (!args.Contains(Argument, StringComparer.Ordinal))
        {
            return false;
        }

        if (args.Length != 1)
        {
            Environment.ExitCode = 2;
            return true;
        }

        try
        {
            await using var input = Console.OpenStandardInput();
            await using var output = Console.OpenStandardOutput();
            var server = new CredentialHelperServer(OperatingSystemCredentialStoreFactory.Create);
            await server.RunAsync(input, output, CancellationToken.None);
        }
        catch (Exception)
        {
            Environment.ExitCode = 1;
        }

        return true;
    }
}
