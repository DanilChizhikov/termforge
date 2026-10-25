using System.Text.RegularExpressions;

namespace DTech.Core.Operations;

public sealed partial class OperationSanitizer : IOperationSanitizer
{
    public const string Redacted = "[redacted]";
    public const int MaxEntries = 32;
    public const int MaxKeyLength = 64;
    public const int MaxValueLength = 1000;
    public const int MaxTextLength = 2000;

    private static readonly string[] SensitiveKeyParts =
    [
        "password",
        "passwd",
        "passphrase",
        "token",
        "secret",
        "privatekey",
        "private_key",
        "private-key",
        "apikey",
        "api_key",
        "api-key",
        "credential",
        "authorization",
    ];

    private static readonly string[] TerminalOutputKeys =
    [
        "output",
        "stdout",
        "stderr",
        "terminal",
        "transcript",
        "scrollback",
    ];

    public IReadOnlyDictionary<string, string> SanitizeDetails(IReadOnlyDictionary<string, string>? details)
    {
        var sanitized = new Dictionary<string, string>(StringComparer.Ordinal);
        if (details is null)
        {
            return sanitized;
        }

        foreach (var entry in details)
        {
            if (sanitized.Count >= MaxEntries)
            {
                break;
            }

            var key = Truncate(entry.Key, MaxKeyLength);
            if (ContainsAny(key, TerminalOutputKeys))
            {
                continue;
            }

            sanitized[key] = ContainsAny(key, SensitiveKeyParts) ? Redacted : SanitizeValue(entry.Value, MaxValueLength);
        }

        return sanitized;
    }

    public string? SanitizeText(string? text)
    {
        return text is null ? null : SanitizeValue(text, MaxTextLength);
    }

    private static string SanitizeValue(string value, int maxLength)
    {
        var withoutKeys = PrivateKeyBlock().Replace(value, Redacted);
        var withoutAssignments = SecretAssignment().Replace(withoutKeys, $"$1={Redacted}");
        return Truncate(withoutAssignments, maxLength);
    }

    private static bool ContainsAny(string key, string[] parts)
    {
        for (var i = 0; i < parts.Length; i++)
        {
            if (key.Contains(parts[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    [GeneratedRegex(@"-----BEGIN [A-Z0-9 ]*PRIVATE KEY(?: BLOCK)?-----.*?(?:-----END [A-Z0-9 ]*PRIVATE KEY(?: BLOCK)?-----|$)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex PrivateKeyBlock();

    [GeneratedRegex(@"\b(password|passwd|passphrase|token|secret|api[_-]?key)\s*[=:]\s*\S+", RegexOptions.IgnoreCase)]
    private static partial Regex SecretAssignment();
}
