using DTech.Entrypoint.Lifecycle;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Entrypoint.Tests.Updates;

[TestClass]
public sealed class UpdateExitOwnershipTests
{
    [TestMethod]
    public async Task Ordinary_exit_cannot_cancel_or_replace_an_update_wait()
    {
        await using var factory = new TermForgeApplicationFactory();
        using var client = factory.CreateClientWithoutCookies();
        var gate = factory.Services.GetRequiredService<WorkAdmissionGate>();
        var exit = factory.Services.GetRequiredService<ApplicationExitCoordinator>();
        gate.TryBeginWaiting().Should().BeTrue();

        exit.TryCancelWaiting().Should().BeFalse();
        exit.TryBegin(ExitTransferPolicy.CancelTransfers).Should().BeFalse();
        gate.State.Should().Be(ApplicationExitState.WaitingForTransfers);
        gate.TryReopen().Should().BeTrue();
    }
}
