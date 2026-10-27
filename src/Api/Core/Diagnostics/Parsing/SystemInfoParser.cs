namespace DTech.Core.Diagnostics.Parsing;

public static class SystemInfoParser
{
    public const string SectionSeparator = "@@@@";

    public const string Script = "uname -sm 2>/dev/null; echo @@@@; cat /etc/os-release 2>/dev/null; echo @@@@; echo \"$SHELL\"";

    private const int MaxSections = 3;
    private const int MaxFieldLength = 80;

    public static SystemInfo Parse(string? output)
    {
        var sections = SplitSections(output);
        var kernel = ParseKernel(sections[0]);
        var distribution = ParseDistribution(sections[1]);
        var shell = ParseShell(sections[2]);
        return new SystemInfo(kernel, distribution, shell);
    }

    private static List<IReadOnlyList<string>> SplitSections(string? output)
    {
        var sections = new List<IReadOnlyList<string>>();
        var current = new List<string>();
        var lines = RemoteText.SplitLines(output);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            if (line != SectionSeparator)
            {
                current.Add(line);
                continue;
            }

            sections.Add(current);
            current = [];
        }

        sections.Add(current);
        while (sections.Count < MaxSections)
        {
            sections.Add([]);
        }

        return sections;
    }

    private static string? ParseKernel(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var sanitized = RemoteText.Sanitize(lines[i], MaxFieldLength);
            if (sanitized.Length > 0)
            {
                return sanitized;
            }
        }

        return null;
    }

    private static string? ParseDistribution(IReadOnlyList<string> lines)
    {
        string? prettyName = null;
        string? name = null;
        string? version = null;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var separatorIndex = line.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex];
            var value = RemoteText.Sanitize(line[(separatorIndex + 1)..].Trim('"', '\''), MaxFieldLength);
            if (value.Length == 0)
            {
                continue;
            }

            switch (key)
            {
                case "PRETTY_NAME":
                    prettyName = value;
                    break;
                case "NAME":
                    name = value;
                    break;
                case "VERSION_ID":
                    version = value;
                    break;
            }
        }

        if (prettyName is not null)
        {
            return prettyName;
        }

        if (name is null)
        {
            return null;
        }

        return version is null ? name : $"{name} {version}";
    }

    private static string? ParseShell(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length == 0)
            {
                continue;
            }

            var isAbsolutePath = line[0] == '/' && !line.Contains(' ', StringComparison.Ordinal);
            return isAbsolutePath ? RemoteText.Sanitize(line, MaxFieldLength) : null;
        }

        return null;
    }
}
