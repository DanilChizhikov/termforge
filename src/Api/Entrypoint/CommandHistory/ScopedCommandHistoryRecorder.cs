using DTech.Core.CommandHistory;

namespace DTech.Entrypoint.CommandHistory;

internal sealed class ScopedCommandHistoryRecorder(IServiceScopeFactory scopeFactory, ILogger<ScopedCommandHistoryRecorder> logger) : ICommandHistoryRecorder
{
    public async Task<bool> RecordAsync(RecordedCommand command, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<CommandHistoryService>();
            return await service.RecordAsync(command, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "A command could not be added to the history.");
            return false;
        }
    }
}
