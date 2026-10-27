using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DTech.Entrypoint.Security.Network;

internal sealed class PasswordHash
{
    public const string AlgorithmName = "pbkdf2-sha256";
    public const int DefaultIterations = 600_000;
    public const int MinimumIterations = 100_000;

    private const int SaltSizeInBytes = 16;
    private const int HashSizeInBytes = 32;
    private const char Separator = '$';
    private const int PartCount = 4;

    private readonly byte[] _salt;
    private readonly byte[] _hash;

    private PasswordHash(int iterations, byte[] salt, byte[] hash)
    {
        Iterations = iterations;
        _salt = salt;
        _hash = hash;
    }

    public int Iterations { get; }

    public static string Create(string password, int iterations)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeInBytes);
        var hash = Derive(password, salt, iterations);
        var saltText = Convert.ToBase64String(salt);
        var hashText = Convert.ToBase64String(hash);
        var iterationsText = iterations.ToString(CultureInfo.InvariantCulture);
        return $"{AlgorithmName}{Separator}{iterationsText}{Separator}{saltText}{Separator}{hashText}";
    }

    public static bool TryParse(string? text, [NotNullWhen(true)] out PasswordHash? passwordHash)
    {
        passwordHash = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmedText = text.Trim();
        var parts = trimmedText.Split(Separator);
        if (parts.Length != PartCount || !string.Equals(parts[0], AlgorithmName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations) || iterations < MinimumIterations)
        {
            return false;
        }

        var salt = TryDecode(parts[2]);
        var hash = TryDecode(parts[3]);
        if (salt is null || hash is null || salt.Length < SaltSizeInBytes || hash.Length != HashSizeInBytes)
        {
            return false;
        }

        passwordHash = new PasswordHash(iterations, salt, hash);
        return true;
    }

    public bool Verify(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        var candidateHash = Derive(password, _salt, Iterations);
        return CryptographicOperations.FixedTimeEquals(candidateHash, _hash);
    }

    private static byte[] Derive(string password, byte[] salt, int iterations)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        return Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, iterations, HashAlgorithmName.SHA256, HashSizeInBytes);
    }

    private static byte[]? TryDecode(string base64)
    {
        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
