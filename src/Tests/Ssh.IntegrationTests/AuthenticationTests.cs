using DTech.Core.Connections;
using DTech.Core.Files;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class AuthenticationTests
{
    [TestMethod]
    public async Task Connect_WithAuthorizedPrivateKeyFile_Succeeds()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var target = cluster.Target.CreateKeyTarget();

        var result = await ConnectAsync(cluster, target);
        await using var client = result.IsSuccess ? result.Value : null;

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [TestMethod]
    public async Task Connect_WithRsaPrivateKeyFile_Succeeds()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var rsaKey = new PrivateKeyAuthentication(cluster.Target.Identity.RsaPrivateKeyPath, null);
        var target = cluster.Target.CreateTarget(rsaKey);

        var result = await ConnectAsync(cluster, target);
        await using var client = result.IsSuccess ? result.Value : null;

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [TestMethod]
    public async Task Connect_WithEncryptedPrivateKeyAndPassphrase_Succeeds()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var encryptedKey = new PrivateKeyAuthentication(cluster.Target.Identity.EncryptedPrivateKeyPath, SshServerContainer.PrivateKeyPassphrase);
        var target = cluster.Target.CreateTarget(encryptedKey);

        var result = await ConnectAsync(cluster, target);
        await using var client = result.IsSuccess ? result.Value : null;

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [TestMethod]
    public async Task Connect_WithEncryptedPrivateKeyWithoutPassphrase_ReturnsPrivateKeyUnusable()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var encryptedKey = new PrivateKeyAuthentication(cluster.Target.Identity.EncryptedPrivateKeyPath, null);
        var target = cluster.Target.CreateTarget(encryptedKey);

        var result = await ConnectAsync(cluster, target);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.PrivateKeyUnusable);
    }

    [TestMethod]
    public async Task Connect_WithUnauthorizedPrivateKeyFile_FailsAuthentication()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var foreignKey = new PrivateKeyAuthentication(cluster.Jump.Identity.PrivateKeyPath, null);
        var target = cluster.Target.CreateTarget(foreignKey);

        var result = await ConnectAsync(cluster, target);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.AuthenticationFailed);
    }

    [TestMethod]
    public async Task Connect_WithPassword_Succeeds()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var password = new PasswordAuthentication(SshServerContainer.Password);
        var target = cluster.Target.CreateTarget(password);

        var result = await ConnectAsync(cluster, target);
        await using var client = result.IsSuccess ? result.Value : null;

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [TestMethod]
    public async Task Connect_WithWrongPassword_FailsAuthenticationWithoutLeakingPassword()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var password = new PasswordAuthentication("wrong-secret-value");
        var target = cluster.Target.CreateTarget(password);

        var result = await ConnectAsync(cluster, target);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.AuthenticationFailed);
        result.Error.Message.Should().NotContain("wrong-secret-value");
        target.ToString().Should().NotContain("wrong-secret-value");
    }

    [TestMethod]
    public async Task Connect_WithIdentityHeldByOpenSshAgent_Succeeds()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var agent = await RequireAgentAsync(cluster);
        await agent.AddIdentityAsync(cluster.Target.Identity.PrivateKeyPath);
        var agentAuthentication = new AgentAuthentication(agent.SocketPath);
        var target = cluster.Target.CreateTarget(agentAuthentication);

        var result = await ConnectAsync(cluster, target);
        await using var client = result.IsSuccess ? result.Value : null;

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }

    [TestMethod]
    public async Task Connect_WithEmptyOpenSshAgent_ReportsAgentUnavailable()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var agent = await RequireAgentAsync(cluster);
        var agentAuthentication = new AgentAuthentication(agent.SocketPath);
        var target = cluster.Target.CreateTarget(agentAuthentication);

        var result = await ConnectAsync(cluster, target);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.AgentUnavailable);
    }

    private static async Task<LocalSshAgent> RequireAgentAsync(SshServerCluster cluster)
    {
        var agent = await LocalSshAgent.TryStartAsync(cluster.KeyDirectory);
        if (agent is null)
        {
            Assert.Inconclusive("ssh-agent is not installed on this machine");
        }

        return agent!;
    }

    private static async Task<SshResult<IRemoteFileClient>> ConnectAsync(SshServerCluster cluster, SshConnectionTarget target)
    {
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        await using var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<IRemoteFileClientFactory>();
        return await factory.ConnectAsync(target, CancellationToken.None);
    }
}
