using DTech.Core.Operations;
using DTech.Core.Transfers;
using DTech.Core.Transfers.Execution;
using DTech.Core.Transfers.Queue;
using DTech.Entrypoint.Realtime;
using DTech.Entrypoint.Transfers;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class TransferServiceCollectionExtensions
{
    public static IServiceCollection AddTransfers(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new TransferOptions();
        var section = configuration.GetSection(TransferOptions.SectionName);
        section.Bind(options);
        services.AddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<RealtimeChannel>();
        services.AddSingleton<TransferQueue>();
        services.AddSingleton<ITransferJournal, ScopedTransferJournal>();
        services.AddSingleton<ITransferConnectionOpener, ScopedTransferConnectionOpener>();
        services.AddSingleton<ITransferNotifier, TransferBroadcaster>();
        services.AddSingleton<LocalTransferPlanner>();
        services.AddSingleton<RemoteTransferPlanner>();
        services.AddSingleton<ITransferStrategy, UploadTransferStrategy>();
        services.AddSingleton<ITransferStrategy, DownloadTransferStrategy>();
        services.AddSingleton<TransferStrategySelector>();
        services.AddSingleton<TransferFileCopier>();
        services.AddSingleton<TransferExecutor>();
        services.AddSingleton<TransferProgressTicker>();
        services.AddSingleton<TransferRunner>();
        services.AddSingleton<TransferEnqueuer>();
        services.AddSingleton<TransferControl>();
        services.AddSingleton<IOperationRetryHandler>(CreateUploadRetryHandler);
        services.AddSingleton<IOperationRetryHandler>(CreateDownloadRetryHandler);
        services.AddHostedService<TransferQueueHostedService>();
        return services;
    }

    private static TransferRetryHandler CreateUploadRetryHandler(IServiceProvider provider)
    {
        var enqueuer = provider.GetRequiredService<TransferEnqueuer>();
        return new TransferRetryHandler(TransferOperationKinds.Upload, enqueuer);
    }

    private static TransferRetryHandler CreateDownloadRetryHandler(IServiceProvider provider)
    {
        var enqueuer = provider.GetRequiredService<TransferEnqueuer>();
        return new TransferRetryHandler(TransferOperationKinds.Download, enqueuer);
    }
}
