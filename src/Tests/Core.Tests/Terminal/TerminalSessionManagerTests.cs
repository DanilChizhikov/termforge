using DTech.Core.Connections;
using DTech.Core.Terminal;
using DTech.Core.Terminal.Sessions;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Terminal;

[TestClass]
public sealed class TerminalSessionManagerTests
{
    private const string Owner = "owner-session-token";
    private const string OtherOwner = "other-session-token";

    private static readonly TerminalSize Size = new(80, 24);
    private static readonly Guid WebConnectionId = Guid.NewGuid();
    private static readonly Guid DbConnectionId = Guid.NewGuid();
    private static readonly SshConnectionTarget Target = new(new SshEndpoint("example.test", 22), "deploy", new AgentAuthentication(null));

    private ITerminalSessionFactory _sessionFactory = null!;
    private List<ITerminalSession> _openedSessions = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _openedSessions = [];
        _sessionFactory = Substitute.For<ITerminalSessionFactory>();
        _sessionFactory.OpenAsync(default!, default).ReturnsForAnyArgs(_ => OpenFakeSession());
    }

    [TestMethod]
    public async Task Open_AssignsUnguessableIdsAndNumbersSessionsPerServer()
    {
        await using var manager = CreateManager(TerminalSessionOptions.Default);
        var server = CreateServer("web");
        var otherServer = CreateServer("db");

        var first = await manager.OpenAsync(CreateRequest(Owner, server), TestContext.CancellationToken);
        var second = await manager.OpenAsync(CreateRequest(Owner, server), TestContext.CancellationToken);
        var third = await manager.OpenAsync(CreateRequest(Owner, otherServer), TestContext.CancellationToken);

        first.Value.Descriptor.Id.Value.Should().MatchRegex("^[0-9a-f]{64}$");
        first.Value.Descriptor.Id.Should().NotBe(second.Value.Descriptor.Id);
        first.Value.Descriptor.SessionNumber.Should().Be(1);
        second.Value.Descriptor.SessionNumber.Should().Be(2);
        third.Value.Descriptor.SessionNumber.Should().Be(1);
        third.Value.Descriptor.ConnectionName.Should().Be("db");
        manager.Count.Should().Be(3);
    }

    [TestMethod]
    public async Task Open_PassesTargetAndInitialSizeToTransport()
    {
        await using var manager = CreateManager(TerminalSessionOptions.Default);
        var size = new TerminalSize(132, 40);
        var request = new TerminalOpenRequest(Owner, CreateServer("web"), size);

        await manager.OpenAsync(request, TestContext.CancellationToken);

        await _sessionFactory.Received(1).OpenAsync(
            Arg.Is<TerminalSessionRequest>(sent => sent.Target == Target && sent.InitialSize == size && sent.TerminalType == TerminalSessionRequest.DefaultTerminalType),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Find_IsBoundToTheOwningBrowserSession()
    {
        await using var manager = CreateManager(TerminalSessionOptions.Default);
        var opened = await manager.OpenAsync(CreateRequest(Owner, CreateServer("web")), TestContext.CancellationToken);
        var id = opened.Value.Descriptor.Id;

        manager.Find(id, Owner).Should().BeSameAs(opened.Value);
        manager.Find(id, OtherOwner).Should().BeNull();
        manager.Find(TerminalSessionId.Create(), Owner).Should().BeNull();
        manager.List(Owner).Should().ContainSingle();
        manager.List(OtherOwner).Should().BeEmpty();
    }

    [TestMethod]
    public async Task Close_ByOwner_DisposesTransportSessionAndForgetsIt()
    {
        await using var manager = CreateManager(TerminalSessionOptions.Default);
        var opened = await manager.OpenAsync(CreateRequest(Owner, CreateServer("web")), TestContext.CancellationToken);
        var id = opened.Value.Descriptor.Id;

        var closedByStranger = await manager.CloseAsync(id, OtherOwner, TerminalCloseReasons.ClosedByUser);
        var closedByOwner = await manager.CloseAsync(id, Owner, TerminalCloseReasons.ClosedByUser);

        closedByStranger.Should().BeFalse();
        closedByOwner.Should().BeTrue();
        await _openedSessions.Single().Received(1).DisposeAsync();
        opened.Value.IsClosed.Should().BeTrue();
        opened.Value.CloseReason.Should().Be(TerminalCloseReasons.ClosedByUser);
        opened.Value.Closing.IsCancellationRequested.Should().BeTrue();
        manager.Count.Should().Be(0);
    }

    [TestMethod]
    public async Task Release_Twice_DisposesTransportSessionOnce()
    {
        await using var manager = CreateManager(TerminalSessionOptions.Default);
        var opened = await manager.OpenAsync(CreateRequest(Owner, CreateServer("web")), TestContext.CancellationToken);

        await manager.ReleaseAsync(opened.Value, TerminalCloseReasons.BrowserDisconnected);
        await manager.ReleaseAsync(opened.Value, TerminalCloseReasons.ClosedByUser);

        await _openedSessions.Single().Received(1).DisposeAsync();
        opened.Value.CloseReason.Should().Be(TerminalCloseReasons.BrowserDisconnected);
    }

    [TestMethod]
    public async Task Open_WhenTransportFails_ReturnsConnectionFailedWithSshKind()
    {
        var refused = new SshError(SshErrorKind.AuthenticationFailed, "Permission denied");
        _sessionFactory.OpenAsync(default!, default).ReturnsForAnyArgs(SshResult<ITerminalSession>.Failure(refused));
        await using var manager = CreateManager(TerminalSessionOptions.Default);

        var opened = await manager.OpenAsync(CreateRequest(Owner, CreateServer("web")), TestContext.CancellationToken);

        opened.IsSuccess.Should().BeFalse();
        opened.Error!.Code.Should().Be(TerminalErrorCode.ConnectionFailed);
        opened.Error.SshErrorKind.Should().Be(SshErrorKind.AuthenticationFailed);
        manager.Count.Should().Be(0);
    }

    [TestMethod]
    public async Task Open_WithInvalidSize_IsRejectedWithoutConnecting()
    {
        await using var manager = CreateManager(TerminalSessionOptions.Default);
        var invalidSize = new TerminalSize(0, 24);
        var request = new TerminalOpenRequest(Owner, CreateServer("web"), invalidSize);

        var opened = await manager.OpenAsync(request, TestContext.CancellationToken);

        opened.Error!.Code.Should().Be(TerminalErrorCode.InvalidRequest);
        await _sessionFactory.DidNotReceiveWithAnyArgs().OpenAsync(default!, default);
    }

    [TestMethod]
    public async Task Open_AboveOwnerLimit_IsRejectedWithoutConnecting()
    {
        var options = TerminalSessionOptions.Default with { MaxSessionsPerOwner = 1 };
        await using var manager = CreateManager(options);
        await manager.OpenAsync(CreateRequest(Owner, CreateServer("web")), TestContext.CancellationToken);

        var rejected = await manager.OpenAsync(CreateRequest(Owner, CreateServer("web")), TestContext.CancellationToken);
        var otherOwner = await manager.OpenAsync(CreateRequest(OtherOwner, CreateServer("web")), TestContext.CancellationToken);

        rejected.Error!.Code.Should().Be(TerminalErrorCode.SessionLimitReached);
        otherOwner.IsSuccess.Should().BeTrue();
        await _sessionFactory.ReceivedWithAnyArgs(2).OpenAsync(default!, default);
    }

    [TestMethod]
    public async Task CloseAll_ClosesEverySessionAndRejectsNewOnes()
    {
        var manager = CreateManager(TerminalSessionOptions.Default);
        var first = await manager.OpenAsync(CreateRequest(Owner, CreateServer("web")), TestContext.CancellationToken);
        var second = await manager.OpenAsync(CreateRequest(OtherOwner, CreateServer("db")), TestContext.CancellationToken);

        await manager.DisposeAsync();
        var afterShutdown = await manager.OpenAsync(CreateRequest(Owner, CreateServer("web")), TestContext.CancellationToken);

        first.Value.CloseReason.Should().Be(TerminalCloseReasons.ShuttingDown);
        second.Value.CloseReason.Should().Be(TerminalCloseReasons.ShuttingDown);
        await _openedSessions[0].Received(1).DisposeAsync();
        await _openedSessions[1].Received(1).DisposeAsync();
        afterShutdown.Error!.Code.Should().Be(TerminalErrorCode.ShuttingDown);
        manager.Count.Should().Be(0);
    }

    [TestMethod]
    public async Task Attach_IsAllowedOnlyOnce()
    {
        await using var manager = CreateManager(TerminalSessionOptions.Default);
        var opened = await manager.OpenAsync(CreateRequest(Owner, CreateServer("web")), TestContext.CancellationToken);

        var firstAttach = opened.Value.TryAttach();
        var secondAttach = opened.Value.TryAttach();

        firstAttach.Should().BeTrue();
        secondAttach.Should().BeFalse();
    }

    [TestMethod]
    public async Task Session_NotAttachedInTime_IsClosed()
    {
        var options = TerminalSessionOptions.Default with { AttachTimeout = TimeSpan.FromMilliseconds(50) };
        await using var manager = CreateManager(options);
        var abandoned = await manager.OpenAsync(CreateRequest(Owner, CreateServer("web")), TestContext.CancellationToken);
        var attached = await manager.OpenAsync(CreateRequest(Owner, CreateServer("web")), TestContext.CancellationToken);
        attached.Value.TryAttach();

        await WaitUntilAsync(() => abandoned.Value.IsClosed);
        await Task.Delay(100, TestContext.CancellationToken);

        abandoned.Value.CloseReason.Should().Be(TerminalCloseReasons.NotAttached);
        attached.Value.IsClosed.Should().BeFalse();
        manager.Count.Should().Be(1);
    }

    private TerminalSessionManager CreateManager(TerminalSessionOptions options)
    {
        return new TerminalSessionManager(_sessionFactory, TimeProvider.System, options);
    }

    private SshResult<ITerminalSession> OpenFakeSession()
    {
        var session = Substitute.For<ITerminalSession>();
        _openedSessions.Add(session);
        return SshResult<ITerminalSession>.Success(session);
    }

    private static TerminalServer CreateServer(string name)
    {
        var connectionId = name == "web" ? WebConnectionId : DbConnectionId;
        return new TerminalServer(connectionId, name, Target);
    }

    private static TerminalOpenRequest CreateRequest(string owner, TerminalServer server)
    {
        return new TerminalOpenRequest(owner, server, Size);
    }

    private async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
