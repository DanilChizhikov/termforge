namespace DTech.Entrypoint.Lifecycle;

internal sealed class GracefulShutdownHostedService(IHostApplicationLifetime lifetime, WorkAdmissionGate gate, GracefulShutdown shutdown) : IHostedLifecycleService
{
    private CancellationTokenRegistration _stoppingRegistration;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stoppingRegistration = lifetime.ApplicationStopping.Register(gate.Close);
        return Task.CompletedTask;
    }

    public Task StartingAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StartedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public async Task StoppingAsync(CancellationToken cancellationToken)
    {
        var running = shutdown.RunAsync();
        await running.WaitAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stoppingRegistration.DisposeAsync();
    }

    public Task StoppedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
