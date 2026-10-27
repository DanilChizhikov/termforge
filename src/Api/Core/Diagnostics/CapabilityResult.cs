namespace DTech.Core.Diagnostics;

public sealed record CapabilityResult(string Id, string Name, CapabilityStatus Status, string Reason)
{
    public static CapabilityResult Available(string id, string name, string reason)
    {
        return new CapabilityResult(id, name, CapabilityStatus.Available, reason);
    }

    public static CapabilityResult Unavailable(string id, string name, string reason)
    {
        return new CapabilityResult(id, name, CapabilityStatus.Unavailable, reason);
    }

    public static CapabilityResult Unknown(string id, string name, string reason)
    {
        return new CapabilityResult(id, name, CapabilityStatus.Unknown, reason);
    }
}
