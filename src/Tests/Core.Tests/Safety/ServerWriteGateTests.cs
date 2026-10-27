using DTech.Core.Safety;
using DTech.Core.Tests.Safety.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Safety;

[TestClass]
public sealed class ServerWriteGateTests
{
    private static readonly Guid ConnectionId = Guid.NewGuid();

    private WriteSafetyFixture _safety = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _safety = new WriteSafetyFixture();
    }

    [TestMethod]
    public async Task Check_WritableServerWithoutDangerousOperation_Allows()
    {
        var intent = new WriteIntent(ConnectionId);

        var denial = await _safety.Gate.CheckAsync(intent, TestContext.CancellationToken);

        denial.Should().BeNull();
    }

    [TestMethod]
    public async Task Check_ReadOnlyServer_DeniesBeforeConsumingToken()
    {
        _safety.MarkReadOnly(ConnectionId);
        var token = _safety.Confirm(DangerousOperation.RemoteDelete, ConnectionId, "/a");
        var intent = new WriteIntent(ConnectionId) { Operation = DangerousOperation.RemoteDelete, Target = "/a", ConfirmationToken = token };

        var denial = await _safety.Gate.CheckAsync(intent, TestContext.CancellationToken);

        denial.Should().Be(WriteDenial.ReadOnlyServer);
        var scope = new ConfirmationScope(DangerousOperation.RemoteDelete, ConnectionId, "/a");
        _safety.Confirmations.TryConsume(scope, token).Should().BeTrue();
    }

    [TestMethod]
    public async Task Check_DangerousOperationWithoutToken_RequiresConfirmation()
    {
        var intent = new WriteIntent(ConnectionId) { Operation = DangerousOperation.ContainerStop, Target = "web" };

        var denial = await _safety.Gate.CheckAsync(intent, TestContext.CancellationToken);

        denial.Should().Be(WriteDenial.ConfirmationRequired);
    }

    [TestMethod]
    public async Task Check_DangerousOperationWithToken_AllowsOnce()
    {
        var token = _safety.Confirm(DangerousOperation.ContainerStop, ConnectionId, "web");
        var intent = new WriteIntent(ConnectionId) { Operation = DangerousOperation.ContainerStop, Target = "web", ConfirmationToken = token };

        var first = await _safety.Gate.CheckAsync(intent, TestContext.CancellationToken);
        var replay = await _safety.Gate.CheckAsync(intent, TestContext.CancellationToken);

        first.Should().BeNull();
        replay.Should().Be(WriteDenial.ConfirmationRequired);
    }
}
