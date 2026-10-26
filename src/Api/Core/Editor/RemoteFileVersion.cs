using System.Security.Cryptography;

namespace DTech.Core.Editor;

public sealed record RemoteFileVersion(DateTime ModifiedUtc, long Size, string Sha256)
{
    public static RemoteFileVersion Of(RemoteFileAttributes attributes, ReadOnlySpan<byte> content)
    {
        var hash = SHA256.HashData(content);
        var hex = Convert.ToHexStringLower(hash);
        var modifiedUtc = DateTime.SpecifyKind(attributes.LastWriteTimeUtc, DateTimeKind.Utc);
        return new RemoteFileVersion(modifiedUtc, content.Length, hex);
    }
}
