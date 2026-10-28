using System.Text.RegularExpressions;
using DTech.Core.Diagnostics;

namespace DTech.Core.Docker;

public readonly partial record struct ImageReference
{
    private ImageReference(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public string Quoted => ShellArgument.Quote(Value);

    public static bool TryParse(string? candidate, out ImageReference reference)
    {
        var trimmed = candidate?.Trim();
        if (trimmed is null || !Pattern().IsMatch(trimmed) || trimmed.Contains("..", StringComparison.Ordinal))
        {
            reference = default;
            return false;
        }

        reference = new ImageReference(trimmed);
        return true;
    }

    public override string ToString()
    {
        return Value;
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._/:@-]{0,254}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
