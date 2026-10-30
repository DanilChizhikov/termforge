using System.Text;

namespace DTech.Core.SettingsTransfer.Package;

public static class ExportKeyNormalizer
{
    public const int MaxInputLength = 256;

    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > MaxInputLength)
        {
            return null;
        }

        var builder = new StringBuilder(ExportKeyAlphabet.SymbolCount);
        for (var i = 0; i < input.Length; i++)
        {
            var symbol = input[i];
            if (symbol == ExportKeyAlphabet.GroupSeparator || char.IsWhiteSpace(symbol))
            {
                continue;
            }

            var canonical = ToCanonical(symbol);
            if (ExportKeyAlphabet.Symbols.IndexOf(canonical) < 0)
            {
                return null;
            }

            builder.Append(canonical);
        }

        return builder.Length == ExportKeyAlphabet.SymbolCount ? builder.ToString() : null;
    }

    private static char ToCanonical(char symbol)
    {
        var upper = char.ToUpperInvariant(symbol);
        return upper switch
        {
            'O' => '0',
            'I' or 'L' => '1',
            _ => upper,
        };
    }
}
