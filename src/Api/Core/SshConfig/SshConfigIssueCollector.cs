namespace DTech.Core.SshConfig;

public sealed class SshConfigIssueCollector(IReadOnlyList<SshConfigIssue> initialIssues)
{
    private readonly Dictionary<(string FilePath, int Line, string Reason), IssueBuilder> _issues = [];

    public void Add(SshConfigDirective directive, string reason, string alias)
    {
        var key = (directive.FilePath, directive.Line, reason);
        if (!_issues.TryGetValue(key, out var builder))
        {
            builder = new IssueBuilder(directive.Keyword);
            _issues[key] = builder;
        }

        if (alias.Length > 0 && !builder.Aliases.Contains(alias))
        {
            builder.Aliases.Add(alias);
        }
    }

    public IReadOnlyList<SshConfigIssue> Build()
    {
        var issues = new List<SshConfigIssue>(initialIssues);
        foreach (var entry in _issues)
        {
            var issue = new SshConfigIssue(entry.Key.FilePath, entry.Key.Line, entry.Value.Directive, entry.Key.Reason)
            {
                AffectedAliases = entry.Value.Aliases,
            };
            issues.Add(issue);
        }

        issues.Sort(CompareByLocation);
        return issues;
    }

    private static int CompareByLocation(SshConfigIssue left, SshConfigIssue right)
    {
        var byFile = string.CompareOrdinal(left.FilePath, right.FilePath);
        return byFile != 0 ? byFile : left.Line.CompareTo(right.Line);
    }

    private sealed class IssueBuilder(string directive)
    {
        public string Directive { get; } = directive;

        public List<string> Aliases { get; } = [];
    }
}
