using DTech.Core.Connections;
using Renci.SshNet;
using Renci.SshNet.Common;
using SshNet.Agent;

namespace DTech.Ssh.Connections;

internal sealed class SshAuthenticationFactory
{
    private static readonly TimeSpan AgentTimeout = TimeSpan.FromSeconds(10);

    public SshResult<SshAuthenticationMaterial> Create(SshConnectionTarget target)
    {
        return target.Authentication switch
        {
            PrivateKeyAuthentication privateKey => CreatePrivateKey(target.UserName, privateKey),
            AgentAuthentication agent => CreateAgent(target.UserName, agent),
            PasswordAuthentication password => CreatePassword(target.UserName, password),
            _ => Unsupported(target.Authentication),
        };
    }

    private static SshResult<SshAuthenticationMaterial> CreatePrivateKey(string userName, PrivateKeyAuthentication authentication)
    {
        PrivateKeyFile keyFile;
        try
        {
            keyFile = new PrivateKeyFile(authentication.PrivateKeyPath, authentication.Passphrase);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SshException or ArgumentException or InvalidOperationException)
        {
            var error = new SshError(SshErrorKind.PrivateKeyUnusable, $"Private key {authentication.PrivateKeyPath} cannot be loaded: {exception.Message}");
            return SshResult<SshAuthenticationMaterial>.Failure(error);
        }

        var method = new PrivateKeyAuthenticationMethod(userName, keyFile);
        var ownedResources = new IDisposable[] { method, keyFile };
        var material = new SshAuthenticationMaterial(method, ownedResources);
        return SshResult<SshAuthenticationMaterial>.Success(material);
    }

    private static SshResult<SshAuthenticationMaterial> CreateAgent(string userName, AgentAuthentication authentication)
    {
        IPrivateKeySource[] identities;
        try
        {
            var agent = CreateAgentClient(authentication.AgentAddress);
            identities = agent.RequestIdentities();
        }
        catch (Exception exception) when (exception is SshAgentException or IOException or System.Net.Sockets.SocketException or TimeoutException or UnauthorizedAccessException)
        {
            var error = new SshError(SshErrorKind.AgentUnavailable, $"SSH agent is not reachable: {exception.Message}");
            return SshResult<SshAuthenticationMaterial>.Failure(error);
        }

        if (identities.Length == 0)
        {
            var error = new SshError(SshErrorKind.AgentUnavailable, "SSH agent holds no identities");
            return SshResult<SshAuthenticationMaterial>.Failure(error);
        }

        var method = new PrivateKeyAuthenticationMethod(userName, identities);
        var ownedResources = new IDisposable[] { method };
        var material = new SshAuthenticationMaterial(method, ownedResources);
        return SshResult<SshAuthenticationMaterial>.Success(material);
    }

    private static SshAgent CreateAgentClient(string? agentAddress)
    {
        if (string.IsNullOrEmpty(agentAddress))
        {
            return new SshAgent(AgentTimeout);
        }

        return new SshAgent(agentAddress, AgentTimeout);
    }

    private static SshResult<SshAuthenticationMaterial> CreatePassword(string userName, PasswordAuthentication authentication)
    {
        var method = new PasswordAuthenticationMethod(userName, authentication.Password);
        var ownedResources = new IDisposable[] { method };
        var material = new SshAuthenticationMaterial(method, ownedResources);
        return SshResult<SshAuthenticationMaterial>.Success(material);
    }

    private static SshResult<SshAuthenticationMaterial> Unsupported(SshAuthentication authentication)
    {
        var authenticationName = authentication.GetType().Name;
        var error = new SshError(SshErrorKind.AuthenticationFailed, $"Authentication {authenticationName} is not supported");
        return SshResult<SshAuthenticationMaterial>.Failure(error);
    }
}
