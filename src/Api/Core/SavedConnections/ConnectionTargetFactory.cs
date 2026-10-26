using DTech.Core.Connections;

namespace DTech.Core.SavedConnections;

public sealed class ConnectionTargetFactory(ConnectionPasswordStore passwordStore)
{
    public async Task<SavedConnectionResult<SshConnectionTarget>> CreateAsync(SavedConnection connection, IReadOnlyList<SavedConnection> all, CancellationToken cancellationToken)
    {
        var authentication = await CreateAuthenticationAsync(connection, cancellationToken);
        if (!authentication.IsSuccess)
        {
            return SavedConnectionResult<SshConnectionTarget>.Failure(authentication.Error);
        }

        var details = connection.Details;
        var endpoint = new SshEndpoint(details.Host, details.Port);
        var target = new SshConnectionTarget(endpoint, details.UserName, authentication.Value);
        if (details.JumpHostId is null)
        {
            return SavedConnectionResult<SshConnectionTarget>.Success(target);
        }

        var jumpConnection = JumpHostGuard.Find(all, details.JumpHostId.Value);
        if (jumpConnection is null)
        {
            var missing = SavedConnectionError.Invalid("The jump host of this connection no longer exists.");
            return SavedConnectionResult<SshConnectionTarget>.Failure(missing);
        }

        var jump = await CreateAsync(jumpConnection, all, cancellationToken);
        if (!jump.IsSuccess)
        {
            var prefixed = jump.Error with { Message = $"Jump host '{jumpConnection.Details.Name}': {jump.Error.Message}" };
            return SavedConnectionResult<SshConnectionTarget>.Failure(prefixed);
        }

        var withJump = target with { JumpHost = jump.Value };
        return SavedConnectionResult<SshConnectionTarget>.Success(withJump);
    }

    private async Task<SavedConnectionResult<SshAuthentication>> CreateAuthenticationAsync(SavedConnection connection, CancellationToken cancellationToken)
    {
        var details = connection.Details;
        switch (details.AuthMethod)
        {
            case ConnectionAuthMethod.Agent:
                var agent = new AgentAuthentication(null);
                return SavedConnectionResult<SshAuthentication>.Success(agent);
            case ConnectionAuthMethod.KeyFile:
                return CreateKeyAuthentication(details);
            default:
                return await CreatePasswordAuthenticationAsync(connection, cancellationToken);
        }
    }

    private static SavedConnectionResult<SshAuthentication> CreateKeyAuthentication(SavedConnectionDraft details)
    {
        var keyPath = ExpandHome(details.PrivateKeyPath!);
        if (!File.Exists(keyPath))
        {
            var missing = SavedConnectionError.Invalid($"The private key file '{keyPath}' does not exist. Check the key file path.");
            return SavedConnectionResult<SshAuthentication>.Failure(missing);
        }

        var authentication = new PrivateKeyAuthentication(keyPath, null);
        return SavedConnectionResult<SshAuthentication>.Success(authentication);
    }

    private async Task<SavedConnectionResult<SshAuthentication>> CreatePasswordAuthenticationAsync(SavedConnection connection, CancellationToken cancellationToken)
    {
        if (!connection.HasPassword)
        {
            var notSet = SavedConnectionError.Invalid("No password is stored for this connection. Edit the connection and enter the password.");
            return SavedConnectionResult<SshAuthentication>.Failure(notSet);
        }

        var password = await passwordStore.ReadAsync(connection.Id, cancellationToken);
        if (!password.IsSuccess)
        {
            return SavedConnectionResult<SshAuthentication>.Failure(password.Error);
        }

        var authentication = new PasswordAuthentication(password.Value);
        return SavedConnectionResult<SshAuthentication>.Success(authentication);
    }

    private static string ExpandHome(string path)
    {
        if (path != "~" && !path.StartsWith("~/", StringComparison.Ordinal) && !path.StartsWith("~\\", StringComparison.Ordinal))
        {
            return path;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, path.Length > 2 ? path[2..] : string.Empty);
    }
}
