using System.Security.Cryptography;
using System.Text;

namespace DTech.HostKeys.Tests.Support;

internal static class HashedHostName
{
    private const int SaltLength = 20;

    public static string Create(string lookupName)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var lookupBytes = Encoding.ASCII.GetBytes(lookupName);
        var hash = HMACSHA1.HashData(salt, lookupBytes);
        var encodedSalt = Convert.ToBase64String(salt);
        var encodedHash = Convert.ToBase64String(hash);
        return $"|1|{encodedSalt}|{encodedHash}";
    }
}
