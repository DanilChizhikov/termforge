namespace DTech.Core.SshConfig;

public sealed class SshConfigParser(SshConfigEnvironment environment)
{
    public SshConfigParseResult Parse(string configPath)
    {
        var loader = new SshConfigDocumentLoader(environment);
        var document = loader.Load(configPath);
        if (!document.RootFound)
        {
            return new SshConfigParseResult(false, [], document.Issues);
        }

        var blocks = SshConfigBlockBuilder.Build(document.Directives);
        var collector = new SshConfigIssueCollector(document.Issues);
        ReportMatchBlocks(document.Directives, collector);
        var resolver = new SshConfigHostResolver(environment, collector);
        var aliases = CollectAliases(blocks);
        var hosts = new List<SshConfigHostEntry>();
        for (var i = 0; i < aliases.Count; i++)
        {
            var entry = resolver.Resolve(aliases[i], blocks);
            hosts.Add(entry);
        }

        var issues = collector.Build();
        return new SshConfigParseResult(true, hosts, issues);
    }

    private static void ReportMatchBlocks(IReadOnlyList<SshConfigDirective> directives, SshConfigIssueCollector collector)
    {
        for (var i = 0; i < directives.Count; i++)
        {
            var directive = directives[i];
            if (string.Equals(directive.Keyword, SshConfigBlockBuilder.MatchKeyword, StringComparison.OrdinalIgnoreCase))
            {
                collector.Add(directive, "Match blocks are not supported; all settings inside the block were ignored.", string.Empty);
            }
        }
    }

    private static List<string> CollectAliases(IReadOnlyList<SshConfigBlock> blocks)
    {
        var aliases = new List<string>();
        for (var blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
        {
            var patterns = blocks[blockIndex].Patterns;
            if (patterns is null || blocks[blockIndex].IsMatchBlock)
            {
                continue;
            }

            for (var patternIndex = 0; patternIndex < patterns.Count; patternIndex++)
            {
                var pattern = patterns[patternIndex];
                if (!SshHostPattern.HasWildcard(pattern) && !aliases.Contains(pattern, StringComparer.OrdinalIgnoreCase))
                {
                    aliases.Add(pattern);
                }
            }
        }

        return aliases;
    }
}
