using DTech.Core.Operations;

namespace DTech.Entrypoint.Operations;

internal sealed class OperationRetentionService(IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<OperationRetentionService> logger) : BackgroundService
{
    internal static OperationRetentionPolicy Policy(DateTime now)
    {
        return new OperationRetentionPolicy(now.AddDays(-30), 10_000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var maintenance = scope.ServiceProvider.GetRequiredService<IOperationMaintenance>();
                    var now = timeProvider.GetUtcNow().UtcDateTime;
                    var policy = Policy(now);
                    await maintenance.PruneAsync(policy, stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, $"Operation journal retention failed: {exception.GetType().Name}");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
