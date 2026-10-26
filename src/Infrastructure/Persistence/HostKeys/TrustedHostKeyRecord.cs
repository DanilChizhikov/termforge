namespace DTech.Persistence.HostKeys;

public sealed class TrustedHostKeyRecord
{
    public long Id { get; set; }

    public required string Host { get; set; }

    public int Port { get; set; }

    public required string Algorithm { get; set; }

    public required string Fingerprint { get; set; }

    public DateTime TrustedAt { get; set; }
}
