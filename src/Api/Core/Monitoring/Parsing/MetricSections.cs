namespace DTech.Core.Monitoring.Parsing;

public sealed class MetricSections
{
    private readonly Dictionary<string, string> _sections;

    private MetricSections(Dictionary<string, string> sections)
    {
        _sections = sections;
    }

    public static MetricSections Parse(string output)
    {
        var sections = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines = output.Split('\n');
        string? current = null;
        var body = new List<string>();
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            if (line.StartsWith(MetricsScript.SectionPrefix, StringComparison.Ordinal))
            {
                Store(sections, current, body);
                current = line[MetricsScript.SectionPrefix.Length..].Trim();
                body = [];
                continue;
            }

            body.Add(line);
        }

        Store(sections, current, body);
        return new MetricSections(sections);
    }

    public string Get(string name)
    {
        return _sections.TryGetValue(name, out var body) ? body : string.Empty;
    }

    private static void Store(Dictionary<string, string> sections, string? name, List<string> body)
    {
        if (name is null)
        {
            return;
        }

        sections[name] = string.Join('\n', body).Trim();
    }
}
