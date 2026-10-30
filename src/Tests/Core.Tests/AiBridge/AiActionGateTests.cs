using DTech.Core.AiBridge;
using DTech.Core.AiBridge.Approvals;
using DTech.Core.Tests.AiBridge.Support;
using FluentAssertions;

namespace DTech.Core.Tests.AiBridge;

[TestClass]
public sealed class AiActionGateTests
{
    private static readonly AiActionDescription Action = new(Guid.NewGuid(), "prod-web", AiActionKind.Command, "reboot");

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Authorize_WhenApprovalsAreOff_AllowsWithoutAsking()
    {
        var graph = new AiBridgeTestGraph();
        graph.DisableApprovals();

        var rejection = await graph.Gate.AuthorizeAsync(Action, TestContext.CancellationToken);

        rejection.Should().BeNull();
        graph.Notifier.Requested.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Authorize_WhenApproved_Allows()
    {
        var graph = new AiBridgeTestGraph();

        var rejection = await graph.Gate.AuthorizeAsync(Action, TestContext.CancellationToken);

        rejection.Should().BeNull();
        graph.Notifier.Requested.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Authorize_WhenDenied_ReturnsDenied()
    {
        var graph = new AiBridgeTestGraph();
        graph.Notifier.Answer = AiApprovalOutcome.Denied;

        var rejection = await graph.Gate.AuthorizeAsync(Action, TestContext.CancellationToken);

        rejection!.Code.Should().Be(AiBridgeErrorCode.Denied);
        rejection.Message.Should().Contain("prod-web");
    }
}
