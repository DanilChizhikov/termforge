using System.Text;
using DTech.Core.Editor;
using DTech.Core.Files.Remote;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Ssh.IntegrationTests.Editor;

internal sealed class EditorSession : IAsyncDisposable
{
    private const long MaxFileBytes = 1024 * 1024;
    private const int MaxBackupsPerFile = 5;

    private readonly ServiceProvider _services;
    private readonly SshServerCluster _cluster;
    private readonly EditorPathResolver _paths;

    private EditorSession(ServiceProvider services, SshServerCluster cluster, IRemoteEditorFileClient client)
    {
        _services = services;
        _cluster = cluster;
        Client = client;
        var limits = new EditorLimits(MaxFileBytes, MaxBackupsPerFile);
        var names = new AuxiliaryFileNames();
        var codec = new TextContentCodec();
        var normalizer = new RemotePathNormalizer();
        _paths = new EditorPathResolver(normalizer);
        var snapshots = new RemoteFileSnapshotReader(limits);
        Documents = new RemoteDocumentReader(snapshots, codec);
        Catalog = new BackupCatalog(names);
        var conflicts = new ConflictDetector(codec);
        var backups = new BackupWriter(names, TimeProvider.System);
        var replacer = new FileReplacer(names);
        var pruner = new BackupPruner(Catalog, limits);
        Saver = new RemoteFileSaver(snapshots, conflicts, backups, replacer, pruner, limits);
        Rollback = new RemoteFileRollback(Catalog, snapshots, Saver);
    }

    public IRemoteEditorFileClient Client { get; }

    public RemoteDocumentReader Documents { get; }

    public BackupCatalog Catalog { get; }

    public RemoteFileSaver Saver { get; }

    public RemoteFileRollback Rollback { get; }

    public static async Task<EditorSession> OpenAsync()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        var services = SshTransportTestServices.Create(verifier, static collection => collection.AddRemoteEditorTransport());
        var factory = services.GetRequiredService<IRemoteEditorFileClientFactory>();
        var target = cluster.Target.CreateKeyTarget();
        var connected = await factory.ConnectAsync(target, CancellationToken.None);
        connected.IsSuccess.Should().BeTrue(connected.Error?.Message);
        return new EditorSession(services, cluster, connected.Value);
    }

    public RemoteFileLocation Locate(string path)
    {
        return _paths.Resolve(path).Value;
    }

    public async Task<string> CreateWorkDirectoryAsync()
    {
        var path = $"{SshServerContainer.HomeDirectory}/editor-{Guid.NewGuid():N}";
        await ExecuteAsync($"mkdir '{path}' && chown {SshServerContainer.UserName}: '{path}'");
        return path;
    }

    public async Task<string> CreateFileAsync(string directory, string name, string text)
    {
        var path = $"{directory}/{name}";
        var content = Encoding.UTF8.GetBytes(text);
        var creation = new RemoteFileCreation(path, content, FileReplacer.NewFileMode);
        var created = await Client.CreateFileAsync(creation, CancellationToken.None);
        created.IsSuccess.Should().BeTrue(created.Error?.Message);
        return path;
    }

    public async Task<string> ReadTextAsync(string path)
    {
        var request = new RemoteReadRequest(path, MaxFileBytes);
        var read = await Client.ReadAsync(request, CancellationToken.None);
        read.IsSuccess.Should().BeTrue(read.Error?.Message);
        return Encoding.UTF8.GetString(read.Value);
    }

    public Task<string> ExecuteAsync(string command)
    {
        return _cluster.Target.ExecuteAsync(command, CancellationToken.None);
    }

    public async Task<RemoteFileVersion> OpenVersionAsync(string path)
    {
        var opened = await Documents.ReadAsync(Client, path, CancellationToken.None);
        opened.IsSuccess.Should().BeTrue(opened.Error?.Message);
        return opened.Value.Version;
    }

    public Task<EditorResult<RemoteSaveResult>> SaveAsync(string path, string text, RemoteFileVersion? expectedVersion)
    {
        var location = Locate(path);
        var content = Encoding.UTF8.GetBytes(text);
        var request = new RemoteSaveRequest(location, content, expectedVersion);
        return Saver.SaveAsync(Client, request, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        await Client.DisposeAsync();
        await _services.DisposeAsync();
    }
}
