namespace DTech.Core.Editor.Sudo;

public sealed class RemoteShellCommand(string text, TimeSpan timeout, int maxOutputBytes)
{
    public string Text { get; } = text;

    public TimeSpan Timeout { get; } = timeout;

    public int MaxOutputBytes { get; } = maxOutputBytes;

    public ReadOnlyMemory<byte> StandardInput { get; init; }
}
