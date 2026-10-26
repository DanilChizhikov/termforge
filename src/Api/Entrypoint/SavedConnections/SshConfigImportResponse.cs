using DTech.Core.SavedConnections;

namespace DTech.Entrypoint.SavedConnections;

public sealed class SshConfigImportResponse
{
    public required string ConfigPath { get; init; }

    public int Created { get; init; }

    public int Updated { get; init; }

    public required IReadOnlyList<SshConfigIssueResponse> Issues { get; init; }

    public static SshConfigImportResponse From(SshConfigImportResult result)
    {
        var issues = new List<SshConfigIssueResponse>(result.Issues.Count);
        for (var i = 0; i < result.Issues.Count; i++)
        {
            var issue = result.Issues[i];
            issues.Add(new SshConfigIssueResponse(issue.FilePath, issue.Line, issue.Directive, issue.Reason, issue.AffectedAliases));
        }

        return new SshConfigImportResponse
        {
            ConfigPath = result.ConfigPath,
            Created = result.Created,
            Updated = result.Updated,
            Issues = issues,
        };
    }
}

public sealed record SshConfigIssueResponse(string FilePath, int Line, string Directive, string Reason, IReadOnlyList<string> AffectedAliases);
