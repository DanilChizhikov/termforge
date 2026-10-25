namespace DTech.Entrypoint.Security;

internal sealed class LoopbackBindingStartupFilter(IConfiguration configuration) : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        var violations = LoopbackBindingValidator.FindNonLoopbackBindings(configuration);
        if (violations.Count > 0)
        {
            var violationList = string.Join("; ", violations);
            throw new InsecureBindingException($"TermForge listens only on loopback addresses. Network access mode is not available. Rejected bindings: {violationList}");
        }

        return next;
    }
}
