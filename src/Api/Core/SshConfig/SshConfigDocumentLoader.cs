namespace DTech.Core.SshConfig;

public sealed class SshConfigDocumentLoader(SshConfigEnvironment environment)
{
    public const int MaxIncludeDepth = 16;
    public const string IncludeKeyword = "Include";

    public SshConfigDocument Load(string rootPath)
    {
        var fullPath = Path.GetFullPath(rootPath);
        var state = new LoadState();
        if (!File.Exists(fullPath))
        {
            return new SshConfigDocument(false, state.Directives, state.Issues);
        }

        state.ActiveFiles.Add(fullPath);
        LoadFile(fullPath, 0, state);
        return new SshConfigDocument(true, state.Directives, state.Issues);
    }

    private void LoadFile(string path, int depth, LoadState state)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var unreadable = new SshConfigIssue(path, 0, "(file)", $"The file could not be read: {exception.Message}");
            state.Issues.Add(unreadable);
            return;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var directive = SshConfigLineParser.Parse(path, i + 1, lines[i]);
            if (directive is null)
            {
                continue;
            }

            if (string.Equals(directive.Keyword, IncludeKeyword, StringComparison.OrdinalIgnoreCase))
            {
                LoadIncludes(directive, depth, state);
                continue;
            }

            state.Directives.Add(directive);
        }
    }

    private void LoadIncludes(SshConfigDirective include, int depth, LoadState state)
    {
        for (var i = 0; i < include.Values.Count; i++)
        {
            var includedFiles = ResolveIncludedFiles(include, include.Values[i], state);
            for (var fileIndex = 0; fileIndex < includedFiles.Count; fileIndex++)
            {
                LoadIncludedFile(include, includedFiles[fileIndex], depth + 1, state);
            }
        }
    }

    private void LoadIncludedFile(SshConfigDirective include, string path, int depth, LoadState state)
    {
        if (depth > MaxIncludeDepth)
        {
            state.Issues.Add(CreateIssue(include, $"Includes are nested deeper than {MaxIncludeDepth} levels; '{path}' was skipped."));
            return;
        }

        if (!state.ActiveFiles.Add(path))
        {
            state.Issues.Add(CreateIssue(include, $"'{path}' includes itself through a loop and was skipped."));
            return;
        }

        LoadFile(path, depth, state);
        state.ActiveFiles.Remove(path);
    }

    private List<string> ResolveIncludedFiles(SshConfigDirective include, string pattern, LoadState state)
    {
        var expanded = SshConfigPathExpander.Expand(pattern, environment.HomeDirectory);
        var rooted = Path.IsPathRooted(expanded) ? expanded : Path.Combine(environment.SshDirectory, expanded);
        var directory = Path.GetDirectoryName(rooted) ?? environment.SshDirectory;
        var filePattern = Path.GetFileName(rooted);
        var files = new List<string>();
        if (directory.Contains('*') || directory.Contains('?'))
        {
            state.Issues.Add(CreateIssue(include, $"Wildcards in directory names are not supported: '{pattern}'."));
            return files;
        }

        if (!filePattern.Contains('*') && !filePattern.Contains('?'))
        {
            AddLiteralFile(include, rooted, state, files);
            return files;
        }

        if (Directory.Exists(directory))
        {
            files.AddRange(Directory.GetFiles(directory, filePattern));
            files.Sort(StringComparer.Ordinal);
        }

        return files;
    }

    private static void AddLiteralFile(SshConfigDirective include, string path, LoadState state, List<string> files)
    {
        if (File.Exists(path))
        {
            files.Add(Path.GetFullPath(path));
            return;
        }

        state.Issues.Add(CreateIssue(include, $"The included file '{path}' was not found."));
    }

    private static SshConfigIssue CreateIssue(SshConfigDirective include, string reason)
    {
        return new SshConfigIssue(include.FilePath, include.Line, IncludeKeyword, reason);
    }

    private sealed class LoadState
    {
        public List<SshConfigDirective> Directives { get; } = [];

        public List<SshConfigIssue> Issues { get; } = [];

        public HashSet<string> ActiveFiles { get; } = new(StringComparer.Ordinal);
    }
}
