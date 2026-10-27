using DTech.Core.Credentials;

namespace DTech.Core.Editor.Sudo;

public sealed class SudoEditorSessionOpener : ISudoEditorSessionOpener
{
    private readonly RemoteShellConnector _connector;
    private readonly SudoPasswordResolver _passwords;
    private readonly SudoPasswordStore _store;
    private readonly SudoShellFactory _shells;
    private readonly SudoAuthenticator _authenticator;

    public SudoEditorSessionOpener(
        RemoteShellConnector connector,
        SudoPasswordResolver passwords,
        SudoPasswordStore store,
        SudoShellFactory shells,
        SudoAuthenticator authenticator)
    {
        _connector = connector;
        _passwords = passwords;
        _store = store;
        _shells = shells;
        _authenticator = authenticator;
    }

    public async Task<EditorResult<RemoteEditorSession>> OpenAsync(Guid connectionId, SudoCredentials credentials, CancellationToken cancellationToken)
    {
        var resolved = await _passwords.ResolveAsync(connectionId, credentials, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return EditorResult<RemoteEditorSession>.Failure(resolved.Error);
        }

        using var password = resolved.Value;
        var connected = await _connector.ConnectAsync(connectionId, cancellationToken);
        if (!connected.IsSuccess)
        {
            return EditorResult<RemoteEditorSession>.Failure(connected.Error);
        }

        var input = password is null ? null : SudoPasswordInput.From(password);
        var shell = _shells.Create(connected.Value.Runner, input);
        var refused = await _authenticator.AuthenticateAsync(shell, cancellationToken);
        if (refused is null)
        {
            refused = await RememberAsync(connectionId, credentials, password, cancellationToken);
        }

        if (refused is not null)
        {
            await shell.DisposeAsync();
            return EditorResult<RemoteEditorSession>.Failure(refused);
        }

        var client = _shells.CreateClient(shell);
        var session = new RemoteEditorSession(client, connected.Value.ServerName);
        return EditorResult<RemoteEditorSession>.Success(session);
    }

    private async Task<EditorError?> RememberAsync(Guid connectionId, SudoCredentials credentials, Secret? password, CancellationToken cancellationToken)
    {
        if (!credentials.RememberPassword || credentials.Password is null || password is null)
        {
            return null;
        }

        return await _store.SaveAsync(connectionId, password, cancellationToken);
    }
}
