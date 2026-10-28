using DTech.Core.Diagnostics.Probes;
using DTech.Core.Docker;
using DTech.Entrypoint.Docker;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class DockerServiceCollectionExtensions
{
    public static IServiceCollection AddDockerManagement(this IServiceCollection services)
    {
        services.TryAddSingleton<DockerProbe>();
        services.AddSingleton<DockerAvailabilityChecker>();
        services.AddSingleton<DockerContainerLister>();
        services.AddSingleton<DockerOutputRelay>();
        services.AddScoped<DockerTargetResolver>();
        services.AddScoped<DockerOperationRecorder>();
        services.AddScoped<DockerOverviewService>();
        services.AddScoped<ContainerActionService>();
        services.AddScoped<ContainerLogService>();
        services.AddScoped<ContainerShellLauncher>();
        services.AddSingleton<ComposeProjectResolver>();
        services.AddScoped<ComposeActionService>();
        services.AddScoped<DockerImageService>();
        services.AddScoped<ImageRemoveService>();
        services.AddScoped<ImagePullService>();
        return services;
    }
}
