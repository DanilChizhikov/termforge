namespace DTech.Core.SshConfig;

public static class SshConfigBlockBuilder
{
    public const string HostKeyword = "Host";
    public const string MatchKeyword = "Match";

    public static IReadOnlyList<SshConfigBlock> Build(IReadOnlyList<SshConfigDirective> directives)
    {
        var blocks = new List<SshConfigBlock>();
        IReadOnlyList<string>? patterns = null;
        var isMatch = false;
        var current = new List<SshConfigDirective>();
        for (var i = 0; i < directives.Count; i++)
        {
            var directive = directives[i];
            var startsHost = IsKeyword(directive, HostKeyword);
            var startsMatch = IsKeyword(directive, MatchKeyword);
            if (!startsHost && !startsMatch)
            {
                current.Add(directive);
                continue;
            }

            blocks.Add(new SshConfigBlock(patterns, isMatch, current));
            current = [];
            isMatch = startsMatch;
            patterns = startsHost ? directive.Values : [];
        }

        blocks.Add(new SshConfigBlock(patterns, isMatch, current));
        return blocks;
    }

    private static bool IsKeyword(SshConfigDirective directive, string keyword)
    {
        return string.Equals(directive.Keyword, keyword, StringComparison.OrdinalIgnoreCase);
    }
}
