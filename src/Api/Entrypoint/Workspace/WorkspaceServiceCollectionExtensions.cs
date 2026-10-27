using DTech.Core.Workspace;
using DTech.Persistence.Workspace;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class WorkspaceServiceCollectionExtensions
{
    public static IServiceCollection AddWorkspacePersistence(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<WorkspaceStateUpgrader>();
        services.AddSingleton<WorkspaceStateReader>();
        services.AddSingleton<WorkspaceStateWriteCoordinator>();
        services.AddScoped<IWorkspaceStateStore, WorkspaceStateStore>();
        services.AddScoped<WorkspaceConnectionFilter>();
        services.AddScoped<WorkspaceStateLoader>();
        services.AddScoped<WorkspaceStateSaver>();
        return services;
    }
}
