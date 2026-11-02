using DTech.Core.Operations;
using DTech.Entrypoint.Operations;
using DTech.Entrypoint.Realtime;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class OperationJournalServiceCollectionExtensions
{
    public static IServiceCollection AddOperationJournal(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<RealtimeChannel>();
        services.TryAddSingleton<IOperationSanitizer, OperationSanitizer>();
        services.TryAddSingleton<IOperationNotifier, OperationBroadcaster>();
        services.TryAddScoped<IOperationJournal, OperationJournal>();
        services.AddScoped<OperationRecoveryService>();
        services.AddHostedService<OperationRetentionService>();
        return services;
    }

    public static async Task RecoverOperationsAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var recovery = scope.ServiceProvider.GetRequiredService<OperationRecoveryService>();
        await recovery.RecoverAsync(cancellationToken);
    }
}
