using System.Text;

namespace DTech.Core.Editor.Sudo;

public sealed class RemoteShellCommandResult(int exitCode, byte[] standardOutput, string standardError)
{
    public int ExitCode { get; } = exitCode;

    public byte[] StandardOutput { get; } = standardOutput;

    public string StandardError { get; } = standardError;

    public bool OutputTruncated { get; init; }

    public bool Succeeded => ExitCode == 0;

    public string StandardOutputText => Encoding.UTF8.GetString(StandardOutput);
}
