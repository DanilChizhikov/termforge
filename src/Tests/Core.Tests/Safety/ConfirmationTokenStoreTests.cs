using DTech.Core.Safety;
using DTech.Core.Tests.Terminal.Recording.Support;
using FluentAssertions;

namespace DTech.Core.Tests.Safety;

[TestClass]
public sealed class ConfirmationTokenStoreTests
{
    private static readonly Guid ConnectionId = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private SettableTimeProvider _time = null!;
    private ConfirmationTokenStore _store = null!;

    [TestInitialize]
    public void Initialize()
    {
        _time = new SettableTimeProvider(Start);
        _store = new ConfirmationTokenStore(_time);
    }

    [TestMethod]
    public void Issue_ReturnsUniqueTokenExpiringAfterLifetime()
    {
        var scope = new ConfirmationScope(DangerousOperation.RemoteDelete, ConnectionId, "/srv/app.log");

        var first = _store.Issue(scope);
        var second = _store.Issue(scope);

        first.Token.Should().NotBe(second.Token);
        first.Token.Should().HaveLength(64);
        first.ExpiresAt.Should().Be(Start + ConfirmationTokenStore.Lifetime);
    }

    [TestMethod]
    public void TryConsume_MatchingScope_SucceedsOnlyOnce()
    {
        var scope = new ConfirmationScope(DangerousOperation.ContainerStop, ConnectionId, "web");
        var grant = _store.Issue(scope);

        var firstUse = _store.TryConsume(scope, grant.Token);
        var secondUse = _store.TryConsume(scope, grant.Token);

        firstUse.Should().BeTrue();
        secondUse.Should().BeFalse();
    }

    [TestMethod]
    public void TryConsume_MissingOrUnknownToken_Fails()
    {
        var scope = new ConfirmationScope(DangerousOperation.FileRollback, ConnectionId, "/etc/hosts");

        _store.TryConsume(scope, null).Should().BeFalse();
        _store.TryConsume(scope, string.Empty).Should().BeFalse();
        _store.TryConsume(scope, "forged").Should().BeFalse();
    }

    [TestMethod]
    public void TryConsume_AfterExpiry_Fails()
    {
        var scope = new ConfirmationScope(DangerousOperation.RemoteDelete, ConnectionId, "/srv/app.log");
        var grant = _store.Issue(scope);
        _time.Now = Start + ConfirmationTokenStore.Lifetime;

        var consumed = _store.TryConsume(scope, grant.Token);

        consumed.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(DangerousOperation.ContainerRestart, "web")]
    [DataRow(DangerousOperation.ContainerStop, "db")]
    public void TryConsume_DifferentOperationOrTarget_FailsAndBurnsToken(DangerousOperation operation, string target)
    {
        var issuedScope = new ConfirmationScope(DangerousOperation.ContainerStop, ConnectionId, "web");
        var grant = _store.Issue(issuedScope);
        var otherScope = new ConfirmationScope(operation, ConnectionId, target);

        var mismatched = _store.TryConsume(otherScope, grant.Token);
        var original = _store.TryConsume(issuedScope, grant.Token);

        mismatched.Should().BeFalse();
        original.Should().BeFalse();
    }

    [TestMethod]
    public void TryConsume_DifferentServer_Fails()
    {
        var issuedScope = new ConfirmationScope(DangerousOperation.RemoteDelete, ConnectionId, "/srv/app.log");
        var grant = _store.Issue(issuedScope);
        var otherServerId = Guid.NewGuid();
        var otherServer = issuedScope with { ConnectionId = otherServerId };

        var consumed = _store.TryConsume(otherServer, grant.Token);

        consumed.Should().BeFalse();
    }
}
