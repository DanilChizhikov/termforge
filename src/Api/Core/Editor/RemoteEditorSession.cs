namespace DTech.Core.Editor;

public sealed class RemoteEditorSession(IRemoteEditorFileClient client, string serverName) : IAsyncDisposable
{
    public IRemoteEditorFileClient Client { get; } = client;

    public string ServerName { get; } = serverName;

    public ValueTask DisposeAsync()
    {
        return Client.DisposeAsync();
    }
}
