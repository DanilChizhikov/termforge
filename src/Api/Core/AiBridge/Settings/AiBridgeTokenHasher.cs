using System.Security.Cryptography;
using System.Text;

namespace DTech.Core.AiBridge.Settings;

public static class AiBridgeTokenHasher
{
    public static string Hash(string token)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(tokenBytes);
        return Convert.ToHexStringLower(hash);
    }

    public static bool Matches(string? expectedHash, string? candidateToken)
    {
        if (string.IsNullOrEmpty(expectedHash) || string.IsNullOrEmpty(candidateToken))
        {
            return false;
        }

        var candidateHash = Hash(candidateToken);
        var expectedBytes = Encoding.ASCII.GetBytes(expectedHash);
        var candidateBytes = Encoding.ASCII.GetBytes(candidateHash);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, candidateBytes);
    }
}
