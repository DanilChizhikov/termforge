namespace DTech.Core.Diagnostics;

public interface ICapabilityProbe
{
    Task<IReadOnlyList<CapabilityResult>> ProbeAsync(CapabilityProbeContext context, CancellationToken cancellationToken);
}
