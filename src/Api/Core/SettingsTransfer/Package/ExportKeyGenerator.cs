using System.Security.Cryptography;
using System.Text;

namespace DTech.Core.SettingsTransfer.Package;

public sealed class ExportKeyGenerator
{
    public string Generate()
    {
        var builder = new StringBuilder(ExportKeyAlphabet.SymbolCount + (ExportKeyAlphabet.SymbolCount / ExportKeyAlphabet.GroupLength));
        for (var i = 0; i < ExportKeyAlphabet.SymbolCount; i++)
        {
            if (i > 0 && i % ExportKeyAlphabet.GroupLength == 0)
            {
                builder.Append(ExportKeyAlphabet.GroupSeparator);
            }

            var index = RandomNumberGenerator.GetInt32(ExportKeyAlphabet.Symbols.Length);
            builder.Append(ExportKeyAlphabet.Symbols[index]);
        }

        return builder.ToString();
    }
}
