namespace DTech.Entrypoint.Lifecycle;

internal sealed class WorkAdmissionShutdownStep(WorkAdmissionGate gate) : IShutdownStep
{
    public ShutdownStage Stage => ShutdownStage.StopAcceptingWork;

    public Task RunAsync(CancellationToken cancellationToken)
    {
        gate.Close();
        return Task.CompletedTask;
    }
}
