using DTech.Core.Updates;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Updates;

internal sealed class UpdateCheckStartupService(IUpdateChecker checker, IOptions<UpdateOptions> options, ILogger<UpdateCheckStartupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.CheckOnStartup)
        {
            return;
        }

        await Task.Yield();
        try
        {
            await checker.Check(false, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogDebug($"Startup update check failed: {exception.GetType().Name}");
        }
    }
}
