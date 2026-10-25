using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace DTech.Entrypoint.Security;

internal static class RandomSecret
{
    private const int SecretSizeInBytes = 32;

    public static string Create()
    {
        var secretBytes = RandomNumberGenerator.GetBytes(SecretSizeInBytes);
        return WebEncoders.Base64UrlEncode(secretBytes);
    }
}
