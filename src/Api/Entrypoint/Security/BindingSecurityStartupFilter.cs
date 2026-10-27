using DTech.Entrypoint.Security.Network;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Security;

internal sealed class BindingSecurityStartupFilter(IConfiguration configuration, IOptions<NetworkAccessOptions> networkAccessOptions, ILogger<BindingSecurityStartupFilter> logger) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        var bindings = LoopbackBindingValidator.FindNonLoopbackBindingDetails(configuration);
        var networkAccess = networkAccessOptions.Value;
        if (!networkAccess.Enabled)
        {
            RejectNonLoopbackBindings(bindings);
            return next;
        }

        var problems = NetworkAccessConfigurationValidator.FindProblems(networkAccess, bindings);
        if (problems.Count > 0)
        {
            var problemList = string.Join("; ", problems);
            throw new NetworkAccessConfigurationException($"Network access mode is enabled but TermForge refuses to start without secure configuration: {problemList}");
        }

        LogNetworkMode(bindings);
        return next;
    }

    private static void RejectNonLoopbackBindings(IReadOnlyList<NonLoopbackBinding> bindings)
    {
        if (bindings.Count == 0)
        {
            return;
        }

        var descriptions = new List<string>(bindings.Count);
        for (var i = 0; i < bindings.Count; i++)
        {
            descriptions.Add(bindings[i].Description);
        }

        var violationList = string.Join("; ", descriptions);
        throw new InsecureBindingException($"TermForge listens only on loopback addresses. Network access mode is not available while NetworkAccess:Enabled is false. Rejected bindings: {violationList}");
    }

    private void LogNetworkMode(IReadOnlyList<NonLoopbackBinding> bindings)
    {
        logger.LogWarning("Network access mode is enabled: TermForge accepts password sign-in from other devices");
        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            if (!binding.IsHttps)
            {
                logger.LogCritical($"INSECURE: {binding.Description} and uses plain HTTP. The password and the session cookie travel unencrypted and can be intercepted on the network. Configure HTTPS.");
            }
        }
    }
}
