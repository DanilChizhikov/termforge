using DTech.Core.Diagnostics.Parsing;

namespace DTech.Core.Diagnostics.Probes;

public sealed class UtilitiesProbe : ICapabilityProbe
{
    public async Task<IReadOnlyList<CapabilityResult>> ProbeAsync(CapabilityProbeContext context, CancellationToken cancellationToken)
    {
        var result = await context.Runner.RunScriptAsync(UtilityListParser.Script, cancellationToken);
        var utilities = UtilityListParser.RequiredUtilities;
        var results = new List<CapabilityResult>(utilities.Count);
        if (!result.IsSuccess)
        {
            var failure = ToolLookup.DescribeFailure("check utilities", result.Error?.Message);
            for (var i = 0; i < utilities.Count; i++)
            {
                var unknown = CreateUnknown(utilities[i], failure);
                results.Add(unknown);
            }

            return results;
        }

        var answers = UtilityListParser.Parse(result.Value.StandardOutput);
        for (var i = 0; i < utilities.Count; i++)
        {
            var utility = utilities[i];
            results.Add(Describe(utility, answers));
        }

        return results;
    }

    private static CapabilityResult Describe(string utility, IReadOnlyDictionary<string, bool> answers)
    {
        var id = $"{CapabilityIds.UtilityPrefix}{utility}";
        var name = $"Utility {utility}";
        if (!answers.TryGetValue(utility, out var isPresent))
        {
            return CapabilityResult.Unknown(id, name, $"The server did not report whether '{utility}' is installed.");
        }

        if (isPresent)
        {
            return CapabilityResult.Available(id, name, $"'{utility}' is installed.");
        }

        return CapabilityResult.Unavailable(id, name, $"'{utility}' is not installed. Install {DescribePackage(utility)} on the server.");
    }

    private static CapabilityResult CreateUnknown(string utility, string reason)
    {
        var id = $"{CapabilityIds.UtilityPrefix}{utility}";
        return CapabilityResult.Unknown(id, $"Utility {utility}", reason);
    }

    private static string DescribePackage(string utility)
    {
        return utility switch
        {
            "free" or "uptime" => "procps (or busybox)",
            "stat" or "df" => "coreutils (or busybox)",
            _ => utility,
        };
    }
}
