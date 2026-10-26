using DTech.Entrypoint.Security;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Entrypoint.Tests.Security;

[TestClass]
public sealed class BrowserSessionStoreTests
{
    private static readonly DateTimeOffset StartTime = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly MovableTimeProvider _time = new(StartTime);

    [TestMethod]
    public void Launch_and_password_sessions_coexist()
    {
        var store = new BrowserSessionStore(_time);
        var lifetime = TimeSpan.FromHours(1);

        var launchSession = store.Create(BrowserSessionKind.Launch, null);
        var passwordSession = store.Create(BrowserSessionKind.Password, lifetime);

        store.Find(launchSession.SessionToken).Should().Be(launchSession);
        store.Find(passwordSession.SessionToken).Should().Be(passwordSession);
        store.Find("forged").Should().BeNull();
        store.Find(null).Should().BeNull();
    }

    [TestMethod]
    public void Session_with_lifetime_expires()
    {
        var store = new BrowserSessionStore(_time);
        var lifetime = TimeSpan.FromHours(1);
        var passwordSession = store.Create(BrowserSessionKind.Password, lifetime);
        var launchSession = store.Create(BrowserSessionKind.Launch, null);

        _time.Advance(lifetime);

        store.Find(passwordSession.SessionToken).Should().BeNull();
        store.Find(launchSession.SessionToken).Should().Be(launchSession);
    }

    [TestMethod]
    public void Removed_session_is_not_found()
    {
        var store = new BrowserSessionStore(_time);
        var session = store.Create(BrowserSessionKind.Password, null);

        store.Remove(session.SessionToken);

        store.Find(session.SessionToken).Should().BeNull();
    }

    [TestMethod]
    public void Oldest_session_is_evicted_when_store_is_full()
    {
        var store = new BrowserSessionStore(_time);
        var oldestSession = store.Create(BrowserSessionKind.Password, null);
        for (var i = 0; i < BrowserSessionStore.MaximumSessionCount; i++)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            store.Create(BrowserSessionKind.Password, null);
        }

        _time.Advance(TimeSpan.FromSeconds(1));
        var newestSession = store.Create(BrowserSessionKind.Password, null);

        store.Find(oldestSession.SessionToken).Should().BeNull();
        store.Find(newestSession.SessionToken).Should().Be(newestSession);
    }
}
