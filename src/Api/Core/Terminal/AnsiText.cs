using System.Text.RegularExpressions;

namespace DTech.Core.Terminal;

public static partial class AnsiText
{
    public static string Strip(string text)
    {
        var withoutOsc = OperatingSystemCommand().Replace(text, string.Empty);
        var withoutCsi = ControlSequence().Replace(withoutOsc, string.Empty);
        var withoutEscapes = SingleEscape().Replace(withoutCsi, string.Empty);
        var withUnixNewLines = withoutEscapes.Replace("\r\n", "\n", StringComparison.Ordinal);
        return OtherControlCharacters().Replace(withUnixNewLines, string.Empty);
    }

    [GeneratedRegex(@"\x1B\][^\x07\x1B]*(?:\x07|\x1B\\)?")]
    private static partial Regex OperatingSystemCommand();

    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]")]
    private static partial Regex ControlSequence();

    [GeneratedRegex(@"\x1B[@-Z\\-_]?")]
    private static partial Regex SingleEscape();

    [GeneratedRegex(@"[\x00-\x08\x0B-\x1F\x7F]")]
    private static partial Regex OtherControlCharacters();
}
