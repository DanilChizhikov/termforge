namespace DTech.Core.Diagnostics;

public sealed record RemoteCommandRequest(string Command, TimeSpan Timeout, int MaxOutputBytes)
{
    public const int DefaultMaxOutputBytes = 64 * 1024;

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

    public static RemoteCommandRequest WithDefaults(string command)
    {
        return new RemoteCommandRequest(command, DefaultTimeout, DefaultMaxOutputBytes);
    }
}
