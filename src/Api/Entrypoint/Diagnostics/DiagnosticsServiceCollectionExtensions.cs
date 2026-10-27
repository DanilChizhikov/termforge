using DTech.Core.Diagnostics;
using DTech.Core.Diagnostics.Probes;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class DiagnosticsServiceCollectionExtensions
{
    public static IServiceCollection AddCapabilityDiagnostics(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<CapabilityReportCache>();
        services.AddSingleton<ICapabilityProbe, CommandExecutionProbe>();
        services.AddSingleton<ICapabilityProbe, SystemProbe>();
        services.AddSingleton<ICapabilityProbe, SftpProbe>();
        services.AddSingleton<ICapabilityProbe, DockerProbe>();
        services.AddSingleton<ICapabilityProbe, SudoProbe>();
        services.AddSingleton<ICapabilityProbe, UtilitiesProbe>();
        services.AddScoped<CapabilityDiagnosticsService>();
        return services;
    }
}
