using DTech.Core.Connections;

namespace DTech.HostKeys.KnownHosts;

internal static class KnownHostsLineParser
{
    private const char CommentPrefix = '#';
    private const char MarkerPrefix = '@';
    private const string RevokedMarker = "@revoked";
    private const string CertificateAuthorityMarker = "@cert-authority";
    private const int RequiredFieldCount = 3;
    private static readonly char[] FieldSeparators = [' ', '\t'];

    public static KnownHostsEntry? Parse(string line)
    {
        var trimmedLine = line.Trim();
        if (trimmedLine.Length == 0 || trimmedLine[0] == CommentPrefix)
        {
            return null;
        }

        var fields = trimmedLine.Split(FieldSeparators, StringSplitOptions.RemoveEmptyEntries);
        var marker = KnownHostsMarker.None;
        var firstField = 0;
        if (fields[0][0] == MarkerPrefix)
        {
            var parsedMarker = ParseMarker(fields[0]);
            if (parsedMarker is null)
            {
                return null;
            }

            marker = parsedMarker.Value;
            firstField = 1;
        }

        if (fields.Length - firstField < RequiredFieldCount)
        {
            return null;
        }

        var hostPatterns = fields[firstField];
        var keyType = fields[firstField + 1];
        var encodedKey = fields[firstField + 2];
        var hostKey = ParseHostKey(encodedKey);
        if (hostKey is null || !string.Equals(hostKey.Algorithm, keyType, StringComparison.Ordinal))
        {
            return null;
        }

        return new KnownHostsEntry(marker, hostPatterns, hostKey);
    }

    private static KnownHostsMarker? ParseMarker(string field)
    {
        return field switch
        {
            RevokedMarker => KnownHostsMarker.Revoked,
            CertificateAuthorityMarker => KnownHostsMarker.CertificateAuthority,
            _ => null,
        };
    }

    private static HostKey? ParseHostKey(string encodedKey)
    {
        try
        {
            var keyBlob = Convert.FromBase64String(encodedKey);
            return HostKey.FromPublicKeyBlob(keyBlob);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
