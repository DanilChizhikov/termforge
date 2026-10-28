using System.Text.RegularExpressions;
using DTech.Core.Diagnostics;

namespace DTech.Core.Docker;

public readonly partial record struct ComposeProjectName
{
    private ComposeProjectName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public string Quoted => ShellArgument.Quote(Value);

    public static bool TryParse(string? candidate, out ComposeProjectName name)
    {
        if (candidate is null || !Pattern().IsMatch(candidate))
        {
            name = default;
            return false;
        }

        name = new ComposeProjectName(candidate);
        return true;
    }

    public override string ToString()
    {
        return Value;
    }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9_-]{0,127}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
