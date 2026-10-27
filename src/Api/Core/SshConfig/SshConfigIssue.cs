namespace DTech.Core.SshConfig;

public sealed record SshConfigIssue(string FilePath, int Line, string Directive, string Reason)
{
    public IReadOnlyList<string> AffectedAliases { get; init; } = [];
}
