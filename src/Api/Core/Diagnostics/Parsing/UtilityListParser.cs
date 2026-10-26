namespace DTech.Core.Diagnostics.Parsing;

public static class UtilityListParser
{
    public static readonly IReadOnlyList<string> RequiredUtilities = ["stat", "df", "free", "uptime"];

    public static string Script => "for t in stat df free uptime; do if command -v \"$t\" >/dev/null 2>&1; then echo \"$t=yes\"; else echo \"$t=no\"; fi; done";

    public static IReadOnlyDictionary<string, bool> Parse(string? output)
    {
        var found = new Dictionary<string, bool>();
        var lines = RemoteText.SplitLines(output);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i].Trim();
            var separatorIndex = line.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            var tool = line[..separatorIndex];
            var answer = line[(separatorIndex + 1)..];
            if (!RequiredUtilities.Contains(tool) || answer is not ("yes" or "no"))
            {
                continue;
            }

            found[tool] = answer == "yes";
        }

        return found;
    }
}
