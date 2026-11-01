using DTech.Core.Connections;
using DTech.Ssh.Connections;
using FluentAssertions;
using Renci.SshNet;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class HostKeyAlgorithmPreferenceTests
{
    [TestMethod]
    [DataRow("ssh-rsa")]
    [DataRow("ecdsa-sha2-nistp256")]
    public void Apply_PrefersKnownAlgorithmAndPreservesAllAlgorithms(string knownAlgorithm)
    {
        var connection = new ConnectionInfo("host", "user", new PasswordAuthenticationMethod("user", "password"));
        var original = connection.HostKeyAlgorithms.Keys.ToArray();
        HostKey[] keys = [new(knownAlgorithm, "SHA256:known")];

        HostKeyAlgorithmPreference.Apply(connection, keys);

        var preferred = original.Where(name => name == knownAlgorithm || (knownAlgorithm == "ssh-rsa" && name is "rsa-sha2-256" or "rsa-sha2-512")).ToArray();
        connection.HostKeyAlgorithms.Keys.Take(preferred.Length).Should().Equal(preferred);
        connection.HostKeyAlgorithms.Keys.Should().BeEquivalentTo(original);
    }
}
