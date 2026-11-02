using DTech.Entrypoint.Lifecycle;
using DTech.Entrypoint.Security;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using System.Security.AccessControl;
using System.Security.Principal;

namespace DTech.Entrypoint.Tests.Security;

[TestClass]
public sealed class BootstrapLifetimeTests
{
    [TestMethod]
    public void Lifetime_starts_at_issuance_and_expired_token_cannot_be_reused_or_renewed()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new MovableTimeProvider(now);
        var token = new BootstrapToken(clock);
        var startup = TimeSpan.FromMinutes(5);
        clock.Advance(startup);
        token.TryConsume("unissued").Should().BeFalse();
        var value = token.Value;
        var lifetime = TimeSpan.FromSeconds(60);

        clock.Advance(lifetime);

        token.TryConsume(value).Should().BeFalse();
        token.Value.Should().Be(value);
        token.TryConsume(value).Should().BeFalse();
    }

    [TestMethod]
    public void Token_is_valid_before_deadline_and_consumes_only_once()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new MovableTimeProvider(now);
        var token = new BootstrapToken(clock);
        var value = token.Value;
        var delay = TimeSpan.FromSeconds(59);
        clock.Advance(delay);

        token.TryConsume("wrong").Should().BeFalse();
        token.TryConsume(value).Should().BeTrue();
        token.TryConsume(value).Should().BeFalse();
    }

    [TestMethod]
    public void Launch_file_is_private_contains_redirect_and_disappears_after_consumption()
    {
        var token = new BootstrapToken(TimeProvider.System);
        using var redirect = new BootstrapRedirectFile(token, TimeProvider.System);
        var destination = new Uri($"http://127.0.0.1:5023/bootstrap#token={token.Value}");

        var address = redirect.Create(destination);

        address.IsFile.Should().BeTrue();
        address.AbsoluteUri.Should().NotContain(token.Value);
        var html = File.ReadAllText(address.LocalPath);
        html.Should().Contain(destination.AbsoluteUri);
        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(address.LocalPath);
            mode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
            var directory = Path.GetDirectoryName(address.LocalPath)!;
            var directoryMode = File.GetUnixFileMode(directory);
            directoryMode.Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        else
        {
            var directoryPath = Path.GetDirectoryName(address.LocalPath)!;
            var directory = new DirectoryInfo(directoryPath);
            var security = directory.GetAccessControl();
            security.AreAccessRulesProtected.Should().BeTrue();
            var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
            using var identity = WindowsIdentity.GetCurrent();
            rules.Count.Should().Be(1);
            var rule = (FileSystemAccessRule)rules[0]!;
            rule.IdentityReference.Should().Be(identity.User);
            rule.AccessControlType.Should().Be(AccessControlType.Allow);
        }

        token.TryConsume(token.Value).Should().BeTrue();

        File.Exists(address.LocalPath).Should().BeFalse();
    }

    [TestMethod]
    public void Launch_file_is_deleted_on_shutdown()
    {
        var token = new BootstrapToken(TimeProvider.System);
        var redirect = new BootstrapRedirectFile(token, TimeProvider.System);
        var destination = new Uri($"http://127.0.0.1:5023/bootstrap#token={token.Value}");
        var address = redirect.Create(destination);

        redirect.Dispose();

        File.Exists(address.LocalPath).Should().BeFalse();
    }

    [TestMethod]
    public void Launch_file_is_deleted_when_its_lifetime_expires()
    {
        var clock = new ExpiryTimeProvider();
        var token = new BootstrapToken(clock);
        using var redirect = new BootstrapRedirectFile(token, clock);
        var destination = new Uri($"http://127.0.0.1:5023/bootstrap#token={token.Value}");
        var address = redirect.Create(destination);

        clock.Expire();

        clock.DueTime.Should().Be(TimeSpan.FromSeconds(60));
        File.Exists(address.LocalPath).Should().BeFalse();
        token.TryConsume(token.Value).Should().BeFalse();
    }

    private sealed class ExpiryTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        private TimerCallback? _callback;
        private object? _state;

        public TimeSpan DueTime { get; private set; }

        public override DateTimeOffset GetUtcNow() => _now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _callback = callback;
            _state = state;
            DueTime = dueTime;
            return new Timer(callback, state, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public void Expire()
        {
            _now += DueTime;
            _callback!(_state);
        }
    }
}
