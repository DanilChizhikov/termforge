namespace DTech.Core.Docker;

public interface IDockerOutputStream : IAsyncDisposable
{
    int? ExitCode { get; }

    ValueTask<string?> ReadAsync(CancellationToken cancellationToken);
}
