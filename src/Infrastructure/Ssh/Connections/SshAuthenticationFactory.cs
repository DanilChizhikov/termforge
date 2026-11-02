using DTech.Core.Connections;
using Renci.SshNet;
using SshNet.Agent;

namespace DTech.Ssh.Connections;

internal sealed class SshAuthenticationFactory
{
    private static readonly TimeSpan AgentTimeout = TimeSpan.FromSeconds(10);

    public async Task<SshResult<SshAuthenticationMaterial>> CreateAsync(SshConnectionTarget target, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            var cancelled = new SshError(SshErrorKind.Cancelled, "Preparing SSH authentication was cancelled");
            return SshResult<SshAuthenticationMaterial>.Failure(cancelled);
        }

        return target.Authentication switch
        {
            PrivateKeyAuthentication privateKey => CreatePrivateKey(target.UserName, privateKey),
            AgentAuthentication agent => await CreateAgentAsync(target.UserName, agent, cancellationToken),
            PasswordAuthentication password => CreatePassword(target.UserName, password),
            _ => Unsupported(target.Authentication),
        };
    }

    private static SshResult<SshAuthenticationMaterial> CreatePrivateKey(string userName, PrivateKeyAuthentication authentication)
    {
        PrivateKeyFile? keyFile = null;
        PrivateKeyAuthenticationMethod? method = null;
        var transferred = false;
        try
        {
            keyFile = new PrivateKeyFile(authentication.PrivateKeyPath, authentication.Passphrase);
            method = new PrivateKeyAuthenticationMethod(userName, keyFile);
            var ownedResources = new IDisposable[] { method, keyFile };
            var material = new SshAuthenticationMaterial(method, ownedResources);
            var result = SshResult<SshAuthenticationMaterial>.Success(material);
            transferred = true;
            return result;
        }
        catch (Exception exception)
        {
            var error = new SshError(SshErrorKind.PrivateKeyUnusable, $"Private key {authentication.PrivateKeyPath} cannot be loaded: {exception.Message}");
            return SshResult<SshAuthenticationMaterial>.Failure(error);
        }
        finally
        {
            if (!transferred)
            {
                try
                {
                    method?.Dispose();
                }
                finally
                {
                    keyFile?.Dispose();
                }
            }
        }
    }

    private static async Task<SshResult<SshAuthenticationMaterial>> CreateAgentAsync(string userName, AgentAuthentication authentication, CancellationToken cancellationToken)
    {
        PrivateKeyAuthenticationMethod? method = null;
        var transferred = false;
        try
        {
            var agent = CreateAgentClient(authentication.AgentAddress);
            var identities = await agent.RequestIdentitiesAsync(cancellationToken);
            if (identities.Length == 0)
            {
                var empty = new SshError(SshErrorKind.AgentUnavailable, "SSH agent holds no identities");
                return SshResult<SshAuthenticationMaterial>.Failure(empty);
            }

            method = new PrivateKeyAuthenticationMethod(userName, identities);
            var ownedResources = new IDisposable[] { method };
            var material = new SshAuthenticationMaterial(method, ownedResources);
            var result = SshResult<SshAuthenticationMaterial>.Success(material);
            transferred = true;
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var cancelled = new SshError(SshErrorKind.Cancelled, "Preparing SSH agent authentication was cancelled");
            return SshResult<SshAuthenticationMaterial>.Failure(cancelled);
        }
        catch (Exception exception)
        {
            var error = new SshError(SshErrorKind.AgentUnavailable, $"SSH agent is not reachable: {exception.Message}");
            return SshResult<SshAuthenticationMaterial>.Failure(error);
        }
        finally
        {
            if (!transferred)
            {
                method?.Dispose();
            }
        }
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
