using DTech.Core.Diagnostics.Parsing;

namespace DTech.Core.Diagnostics.Probes;

public sealed class SystemProbe : ICapabilityProbe
{
    private const string Name = "Operating system and shell";

    public async Task<IReadOnlyList<CapabilityResult>> ProbeAsync(CapabilityProbeContext context, CancellationToken cancellationToken)
    {
        var result = await context.Runner.RunScriptAsync(SystemInfoParser.Script, cancellationToken);
        if (!result.IsSuccess)
        {
            var failure = ToolLookup.DescribeFailure("run the system check", result.Error?.Message);
            return [CapabilityResult.Unknown(CapabilityIds.System, Name, failure)];
        }

        var info = SystemInfoParser.Parse(result.Value.StandardOutput);
        if (!info.IsKnown)
        {
            var unreadable = CapabilityResult.Unknown(CapabilityIds.System, Name, "The server did not report its operating system. It may not be a Unix-like system.");
            return [unreadable];
        }

        var available = CapabilityResult.Available(CapabilityIds.System, Name, info.Describe());
        return [available];
    }
}
