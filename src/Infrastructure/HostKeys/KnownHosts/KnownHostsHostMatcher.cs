using System.Security.Cryptography;
using System.Text;
using DTech.Core.Connections;

namespace DTech.HostKeys.KnownHosts;

internal static class KnownHostsHostMatcher
{
    private const string HashedPrefix = "|1|";
    private const char HashedSeparator = '|';
    private const char PatternSeparator = ',';
    private const char NegationPrefix = '!';
    private const int HashedPartCount = 4;

    public static string CreateLookupName(SshEndpoint endpoint)
    {
        var host = endpoint.Host.ToLowerInvariant();
        return endpoint.Port == SshEndpoint.DefaultPort ? host : $"[{host}]:{endpoint.Port}";
    }

    public static bool Matches(string hostPatterns, SshEndpoint endpoint)
    {
        var lookupName = CreateLookupName(endpoint);
        if (hostPatterns.StartsWith(HashedPrefix, StringComparison.Ordinal))
        {
            return MatchesHashed(hostPatterns, lookupName);
        }

        return MatchesPatternList(hostPatterns, lookupName);
    }

    private static bool MatchesHashed(string hashedHost, string lookupName)
    {
        var parts = hashedHost.Split(HashedSeparator);
        if (parts.Length != HashedPartCount)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var lookupBytes = Encoding.ASCII.GetBytes(lookupName);
        var actualHash = HMACSHA1.HashData(salt, lookupBytes);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static bool MatchesPatternList(string hostPatterns, string lookupName)
    {
        var patterns = hostPatterns.Split(PatternSeparator, StringSplitOptions.RemoveEmptyEntries);
        var isMatched = false;
        for (var i = 0; i < patterns.Length; i++)
        {
            var pattern = patterns[i];
            var isNegated = pattern[0] == NegationPrefix;
            var normalizedPattern = (isNegated ? pattern[1..] : pattern).ToLowerInvariant();
            if (!WildcardPattern.Matches(normalizedPattern, lookupName))
            {
                continue;
            }

            if (isNegated)
            {
                return false;
            }

            isMatched = true;
        }

        return isMatched;
    }
}
