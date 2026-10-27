namespace DTech.Entrypoint.Lifecycle;

internal interface IShutdownStep
{
    ShutdownStage Stage { get; }

    Task RunAsync(CancellationToken cancellationToken);
}
