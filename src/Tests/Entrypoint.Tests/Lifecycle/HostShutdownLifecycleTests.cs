using DTech.Entrypoint.Lifecycle;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Tests.Lifecycle;

[TestClass]
public sealed class HostShutdownLifecycleTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task StopSignal_ClosesAdmissionWithoutBlockingAndHostStopAwaitsTheStages()
    {
        var step = new GatedShutdownStep();
        var builder = Host.CreateApplicationBuilder();
        builder.Services.Configure<LifecycleOptions>(options => options.ShutdownTimeoutSeconds = 20);
        builder.Services.AddSingleton<WorkAdmissionGate>();
        builder.Services.AddSingleton<GracefulShutdown>();
        builder.Services.AddSingleton<IShutdownStep>(step);
        builder.Services.AddHostedService<GracefulShutdownHostedService>();
        using var host = builder.Build();
        await host.StartAsync(TestContext.CancellationToken);
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        var gate = host.Services.GetRequiredService<WorkAdmissionGate>();
        var signal = Task.Run(lifetime.StopApplication, TestContext.CancellationToken);
        Task stopping = Task.CompletedTask;
        try
        {
            await signal.WaitAsync(TestTimeout, TestContext.CancellationToken);
            gate.IsAcceptingWork.Should().BeFalse();
            stopping = host.StopAsync(TestContext.CancellationToken);
            await step.Entered.Task.WaitAsync(TestTimeout, TestContext.CancellationToken);

            stopping.IsCompleted.Should().BeFalse();
        }
        finally
        {
            step.Release.TrySetResult();
            await signal.WaitAsync(TestTimeout, TestContext.CancellationToken);
            await stopping.WaitAsync(TestTimeout, TestContext.CancellationToken);
        }

        step.Calls.Should().Be(1);
    }

    [TestMethod]
    public async Task HostShutdownBudget_LeavesTimeAfterTheGracefulStages()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var hostOptions = factory.Services.GetRequiredService<IOptions<HostOptions>>();
        var shutdown = factory.Services.GetRequiredService<GracefulShutdown>();

        hostOptions.Value.ShutdownTimeout.Should().Be(shutdown.Timeout + TimeSpan.FromSeconds(5));
    }

    private sealed class GatedShutdownStep : IShutdownStep
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public ShutdownStage Stage => ShutdownStage.FlushWorkspace;

        public Task RunAsync(CancellationToken cancellationToken)
        {
            Calls++;
            Entered.TrySetResult();
            return Release.Task;
        }
    }
}
