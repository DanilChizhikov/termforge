using System.Security.Cryptography;
using System.Text;

namespace DTech.Entrypoint.Security;

internal static class SecretComparer
{
    public static bool AreEqual(string expected, string? candidate)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var candidateBytes = Encoding.UTF8.GetBytes(candidate);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, candidateBytes);
    }
}
