using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DTech.Core.Transfers.Execution;

public static class TransferPartialName
{
    public const string Suffix = ".part";

    private const int MaxBaseNameLength = 200;
    private const int KeyLength = 16;

    public static string Create(TransferPartialIdentity identity, string targetName)
    {
        var key = CreateKey(identity);
        var baseName = targetName.Length > MaxBaseNameLength ? targetName[..MaxBaseNameLength] : targetName;
        return $".{baseName}.{key}{Suffix}";
    }

    private static string CreateKey(TransferPartialIdentity identity)
    {
        var modifiedTicks = identity.SourceModifiedUtc.Ticks.ToString(CultureInfo.InvariantCulture);
        var size = identity.SourceSize.ToString(CultureInfo.InvariantCulture);
        var material = $"{identity.Direction}|{identity.ConnectionId}|{identity.SourcePath}|{identity.TargetPath}|{size}|{modifiedTicks}";
        var bytes = Encoding.UTF8.GetBytes(material);
        var hash = SHA256.HashData(bytes);
        var hex = Convert.ToHexStringLower(hash);
        return hex[..KeyLength];
    }
}
