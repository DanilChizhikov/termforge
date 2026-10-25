namespace DTech.Core.SshConfig;

public sealed record SshConfigBlock(IReadOnlyList<string>? Patterns, bool IsMatchBlock, IReadOnlyList<SshConfigDirective> Directives)
{
    public bool AppliesTo(string alias)
    {
        if (IsMatchBlock)
        {
            return false;
        }

        return Patterns is null || SshHostPattern.MatchesAny(Patterns, alias);
    }
}
