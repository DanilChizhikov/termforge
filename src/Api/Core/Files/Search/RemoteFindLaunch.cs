namespace DTech.Core.Files.Search;

public sealed record RemoteFindLaunch(IFileSearchExecution? Execution, string? FallbackReason)
{
    public static RemoteFindLaunch Started(IFileSearchExecution execution)
    {
        return new RemoteFindLaunch(execution, null);
    }

    public static RemoteFindLaunch Unavailable(string reason)
    {
        return new RemoteFindLaunch(null, reason);
    }
}
