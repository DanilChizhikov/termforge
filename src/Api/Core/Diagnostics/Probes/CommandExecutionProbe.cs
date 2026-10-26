namespace DTech.Core.Diagnostics.Probes;

public sealed class CommandExecutionProbe : ICapabilityProbe
{
    private const string Name = "SSH command execution";
    private const string Marker = "termforge-probe";

    public async Task<IReadOnlyList<CapabilityResult>> ProbeAsync(CapabilityProbeContext context, CancellationToken cancellationToken)
    {
        var result = await context.Runner.RunScriptAsync($"echo {Marker}", cancellationToken);
        if (!result.IsSuccess)
        {
            var failure = ToolLookup.DescribeFailure("execute a command over SSH", result.Error?.Message);
            return [CapabilityResult.Unavailable(CapabilityIds.CommandExecution, Name, $"{failure}. The server may restrict SSH sessions to a forced command or disable exec channels.")];
        }

        var works = result.Value.Succeeded && result.Value.StandardOutput.Contains(Marker, StringComparison.Ordinal);
        if (works)
        {
            return [CapabilityResult.Available(CapabilityIds.CommandExecution, Name, "Commands run over the SSH exec channel.")];
        }

        var unexpected = CapabilityResult.Unavailable(CapabilityIds.CommandExecution, Name, "The server accepted the command but did not run it as expected. Check for a forced command or restricted shell for this user.");
        return [unexpected];
    }
}
