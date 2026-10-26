using DTech.Core.Diagnostics;

namespace DTech.Entrypoint.Diagnostics;

public sealed class CapabilityResponse
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Status { get; init; }

    public required string Reason { get; init; }

    public static CapabilityResponse From(CapabilityResult result)
    {
        return new CapabilityResponse
        {
            Id = result.Id,
            Name = result.Name,
            Status = result.Status.ToString(),
            Reason = result.Reason,
        };
    }
}
