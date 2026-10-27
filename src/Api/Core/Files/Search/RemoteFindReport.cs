namespace DTech.Core.Files.Search;

public sealed record RemoteFindReport(IReadOnlyList<FileSearchMatch> Matches, IReadOnlyList<FileSearchWarning> Warnings, FileSearchOutcome Outcome)
{
    public string? FailureMessage { get; init; }
}
