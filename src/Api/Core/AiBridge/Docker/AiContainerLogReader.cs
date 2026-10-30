using System.Text;
using DTech.Core.Docker;

namespace DTech.Core.AiBridge.Docker;

public sealed class AiContainerLogReader(AiServerAccessPolicy accessPolicy, ContainerLogService logService)
{
    public const int MaxCharacters = 256 * 1024;

    public async Task<AiBridgeResult<AiContainerLogs>> ReadAsync(AiContainerLogRequest request, CancellationToken cancellationToken)
    {
        var access = await accessPolicy.CheckAsync(request.ConnectionId, cancellationToken);
        if (!access.IsSuccess)
        {
            return AiBridgeResult<AiContainerLogs>.Failure(access.Error);
        }

        var options = ContainerLogOptions.Create(request.Tail, false);
        var logRequest = new ContainerLogRequest(request.ConnectionId, request.Container, options);
        var opened = await logService.OpenAsync(logRequest, cancellationToken);
        if (!opened.IsSuccess)
        {
            var failure = AiBridgeErrorMapper.FromDocker(opened.Error);
            return AiBridgeResult<AiContainerLogs>.Failure(failure);
        }

        await using var stream = opened.Value;
        var collected = await CollectAsync(stream, cancellationToken);
        var logs = new AiContainerLogs(request.Container!, collected.Text, collected.Truncated);
        return AiBridgeResult<AiContainerLogs>.Success(logs);
    }

    private static async Task<(string Text, bool Truncated)> CollectAsync(ContainerLogStream stream, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var chunk = await stream.ReadAsync(cancellationToken);
        while (chunk is not null)
        {
            var remaining = MaxCharacters - builder.Length;
            if (chunk.Length > remaining)
            {
                builder.Append(chunk, 0, remaining);
                return (builder.ToString(), true);
            }

            builder.Append(chunk);
            chunk = await stream.ReadAsync(cancellationToken);
        }

        return (builder.ToString(), false);
    }
}
