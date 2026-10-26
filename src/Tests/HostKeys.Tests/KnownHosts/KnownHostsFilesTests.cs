using DTech.HostKeys.KnownHosts;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace DTech.HostKeys.Tests.KnownHosts;

[TestClass]
public sealed class KnownHostsFilesTests
{
    [TestMethod]
    public void Resolve_WithoutConfiguration_UsesUserKnownHosts()
    {
        var files = Create();

        var paths = files.Resolve();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expectedPath = Path.Combine(home, ".ssh", "known_hosts");
        paths.Should().Equal(expectedPath);
    }

    [TestMethod]
    public void Resolve_ExpandsHomePrefix()
    {
        var files = Create("~/custom/known_hosts", "/etc/ssh/ssh_known_hosts");

        var paths = files.Resolve();

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var expandedPath = Path.Combine(home, "custom/known_hosts");
        paths.Should().Equal(expandedPath, "/etc/ssh/ssh_known_hosts");
    }

    private static KnownHostsFiles Create(params string[] configuredPaths)
    {
        var options = new HostKeyVerificationOptions
        {
            KnownHostsFiles = [.. configuredPaths],
        };
        var wrappedOptions = Options.Create(options);
        return new KnownHostsFiles(wrappedOptions);
    }
}
