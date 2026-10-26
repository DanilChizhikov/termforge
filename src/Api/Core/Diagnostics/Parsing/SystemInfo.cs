namespace DTech.Core.Diagnostics.Parsing;

public sealed record SystemInfo(string? Kernel, string? Distribution, string? Shell)
{
    public bool IsKnown => Kernel is not null || Distribution is not null;

    public string Describe()
    {
        var parts = new List<string>();
        if (Distribution is not null)
        {
            parts.Add(Distribution);
        }

        if (Kernel is not null)
        {
            parts.Add(Kernel);
        }

        if (Shell is not null)
        {
            parts.Add($"shell {Shell}");
        }

        return string.Join(", ", parts);
    }
}
