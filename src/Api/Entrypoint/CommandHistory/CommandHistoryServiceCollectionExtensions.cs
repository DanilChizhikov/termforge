using DTech.Core.CommandHistory;
using DTech.Entrypoint.CommandHistory;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class CommandHistoryServiceCollectionExtensions
{
    public static IServiceCollection AddCommandHistory(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<CommandExclusionSettings>();
        services.AddScoped<CommandHistoryService>();
        services.AddSingleton<ICommandHistoryRecorder, ScopedCommandHistoryRecorder>();
        services.AddSingleton<CommandCaptureSessionFactory>();
        return services;
    }
}
