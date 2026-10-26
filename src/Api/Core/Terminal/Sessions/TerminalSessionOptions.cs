namespace DTech.Core.Terminal.Sessions;

public sealed record TerminalSessionOptions(int MaxSessionsPerOwner, TimeSpan AttachTimeout)
{
    public static TerminalSessionOptions Default { get; } = new(32, TimeSpan.FromSeconds(30));
}
