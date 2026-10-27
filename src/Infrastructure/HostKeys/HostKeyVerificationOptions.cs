namespace DTech.HostKeys;

public sealed class HostKeyVerificationOptions
{
    public const string SectionName = "HostKeys";

    public static readonly TimeSpan DefaultDecisionTimeout = TimeSpan.FromMinutes(2);

    public List<string> KnownHostsFiles { get; set; } = [];

    public TimeSpan DecisionTimeout { get; set; } = DefaultDecisionTimeout;
}
