using DTech.Core.AiBridge;
using DTech.Core.Tests.AiBridge.Support;
using FluentAssertions;

namespace DTech.Core.Tests.AiBridge;

[TestClass]
public sealed class AiServerAccessPolicyTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ListAccessible_ReturnsOnlyServersWithAiAccess()
    {
        var graph = new AiBridgeTestGraph();
        var open = await graph.AddServerAsync("open", TestContext.CancellationToken);
        await graph.AddServerWithoutAiAccessAsync("closed", TestContext.CancellationToken);

        var listed = await graph.AccessPolicy.ListAccessibleAsync(TestContext.CancellationToken);

        listed.Value.Should().ContainSingle().Which.Id.Should().Be(open.Id);
    }

    [TestMethod]
    public async Task Check_WhenBridgeDisabled_FailsWithBridgeDisabled()
    {
        var graph = new AiBridgeTestGraph();
        var server = await graph.AddServerAsync("open", TestContext.CancellationToken);
        graph.DisableBridge();

        var checkedServer = await graph.AccessPolicy.CheckAsync(server.Id, TestContext.CancellationToken);
        var listed = await graph.AccessPolicy.ListAccessibleAsync(TestContext.CancellationToken);

        checkedServer.Error!.Code.Should().Be(AiBridgeErrorCode.BridgeDisabled);
        listed.Error!.Code.Should().Be(AiBridgeErrorCode.BridgeDisabled);
    }

    [TestMethod]
    public async Task Check_ServerWithoutAiAccess_FailsWithAiAccessDisabled()
    {
        var graph = new AiBridgeTestGraph();
        var server = await graph.AddServerWithoutAiAccessAsync("closed", TestContext.CancellationToken);

        var checkedServer = await graph.AccessPolicy.CheckAsync(server.Id, TestContext.CancellationToken);

        checkedServer.Error!.Code.Should().Be(AiBridgeErrorCode.AiAccessDisabled);
    }

    [TestMethod]
    public async Task Check_UnknownServer_FailsWithServerNotFound()
    {
        var graph = new AiBridgeTestGraph();

        var checkedServer = await graph.AccessPolicy.CheckAsync(Guid.NewGuid(), TestContext.CancellationToken);

        checkedServer.Error!.Code.Should().Be(AiBridgeErrorCode.ServerNotFound);
    }

    [TestMethod]
    public async Task CheckWritable_ReadOnlyServer_FailsWithReadOnlyServer()
    {
        var graph = new AiBridgeTestGraph();
        var server = await graph.AddReadOnlyServerAsync("locked", TestContext.CancellationToken);

        var readable = await graph.AccessPolicy.CheckAsync(server.Id, TestContext.CancellationToken);
        var writable = await graph.AccessPolicy.CheckWritableAsync(server.Id, TestContext.CancellationToken);

        readable.IsSuccess.Should().BeTrue();
        writable.Error!.Code.Should().Be(AiBridgeErrorCode.ReadOnlyServer);
    }
}
