using DTech.Core.Terminal.Recording;

namespace DTech.Entrypoint.Recordings;

internal sealed class TerminalRecordingStartupService(IServiceScopeFactory scopeFactory, TerminalRecordingRegistry registry, ILogger<TerminalRecordingStartupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<TerminalRecordingSettingsService>();
            var retention = scope.ServiceProvider.GetRequiredService<TerminalRecordingRetention>();
            var settings = await settingsService.GetAsync(cancellationToken);
            var deleted = await retention.PruneAsync(settings, cancellationToken);
            if (deleted > 0)
            {
                logger.LogInformation($"Deleted {deleted} terminal recordings past the retention limits");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning($"Terminal recording cleanup failed: {exception.Message}");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await registry.StopAllAsync(TerminalRecordingEndReason.SessionClosed);
    }
}
