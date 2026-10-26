namespace DTech.Core.Diagnostics;

public sealed record RemoteCommandResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool OutputTruncated { get; init; }

    public bool Succeeded => ExitCode == 0;

    public string CombinedOutput => $"{StandardOutput}\n{StandardError}";
}
