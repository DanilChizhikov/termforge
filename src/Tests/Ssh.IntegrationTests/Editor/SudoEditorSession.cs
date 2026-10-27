using System.Text;
using DTech.Core.Credentials;
using DTech.Core.Editor;
using DTech.Core.Editor.Sudo;
using DTech.Core.Files.Remote;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Ssh.IntegrationTests.Editor;

internal sealed class SudoEditorSession : IAsyncDisposable
{
    private const long MaxFileBytes = 1024 * 1024;
    private const int MaxBackupsPerFile = 5;

    private readonly ServiceProvider _services;
    private readonly SshServerCluster _cluster;
    private readonly EditorPathResolver _paths;
    private readonly SudoShellFactory _shells;
    private readonly SudoAuthenticator _authenticator;
    private readonly List<IAsyncDisposable> _opened = [];

    private SudoEditorSession(ServiceProvider services, SshServerCluster cluster)
    {
        _services = services;
        _cluster = cluster;
        var limits = new EditorLimits(MaxFileBytes, MaxBackupsPerFile);
        var names = new AuxiliaryFileNames();
        var codec = new TextContentCodec();
        var normalizer = new RemotePathNormalizer();
        var commands = new SudoCommandBuilder();
        var classifier = new SudoResponseClassifier();
        _paths = new EditorPathResolver(normalizer);
        _shells = new SudoShellFactory(commands, limits);
        _authenticator = new SudoAuthenticator(classifier);
        Probe = new SudoAvailabilityProbe(commands, classifier);
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

    public SudoAvailabilityProbe Probe { get; }

    public RemoteDocumentReader Documents { get; }

    public BackupCatalog Catalog { get; }

    public RemoteFileSaver Saver { get; }

    public RemoteFileRollback Rollback { get; }

    public static async Task<SudoEditorSession> OpenAsync()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        var services = SshTransportTestServices.Create(verifier, static collection => collection.AddRemoteShellTransport());
        return new SudoEditorSession(services, cluster);
    }

    public async Task<IRemoteShellCommandRunner> ConnectAsync(string userName, string loginPassword)
    {
        var factory = _services.GetRequiredService<IRemoteShellCommandRunnerFactory>();
        var target = _cluster.Target.CreatePasswordTarget(userName, loginPassword);
        var connected = await factory.ConnectAsync(target, CancellationToken.None);
        connected.IsSuccess.Should().BeTrue(connected.Error?.Message);
        _opened.Add(connected.Value);
        return connected.Value;
    }

    public async Task<EditorResult<IRemoteEditorFileClient>> ElevateAsync(string userName, string loginPassword, string? sudoPassword)
    {
        var runner = await ConnectAsync(userName, loginPassword);
        var input = CreateInput(sudoPassword);
        var shell = _shells.Create(runner, input);
        _opened.Add(shell);
        var refused = await _authenticator.AuthenticateAsync(shell, CancellationToken.None);
        if (refused is not null)
        {
            return EditorResult<IRemoteEditorFileClient>.Failure(refused);
        }

        IRemoteEditorFileClient client = _shells.CreateClient(shell);
        return EditorResult<IRemoteEditorFileClient>.Success(client);
    }

    public async Task<IRemoteEditorFileClient> ElevateSudoerAsync()
    {
        var elevated = await ElevateAsync(SshServerContainer.SudoUserName, SshServerContainer.SudoPassword, SshServerContainer.SudoPassword);
        elevated.IsSuccess.Should().BeTrue(elevated.Error?.Message);
        return elevated.Value;
    }

    public RemoteFileLocation Locate(string path)
    {
        return _paths.Resolve(path).Value;
    }

    public async Task<string> CreateProtectedDirectoryAsync()
    {
        var path = $"/srv/termforge-sudo-{Guid.NewGuid():N}";
        await ExecuteAsync($"mkdir -p '{path}' && chown root:root '{path}' && chmod 755 '{path}'");
        return path;
    }

    public async Task<string> CreateProtectedFileAsync(ProtectedFile file)
    {
        var path = $"{file.Directory}/{file.Name}";
        var bytes = Encoding.UTF8.GetBytes(file.Text);
        var encoded = Convert.ToBase64String(bytes);
        var quotedPath = ShellQuoting.Quote(path);
        await ExecuteAsync($"printf '%s' '{encoded}' | base64 -d > {quotedPath} && chown {file.Owner} {quotedPath} && chmod {file.Mode} {quotedPath}");
        return path;
    }

    public Task<string> ExecuteAsync(string command)
    {
        return _cluster.Target.ExecuteAsync(command, CancellationToken.None);
    }

    public async Task<string> ReadAsRootAsync(string path)
    {
        var quotedPath = ShellQuoting.Quote(path);
        var encoded = await ExecuteAsync($"base64 {quotedPath} | tr -d '\\n'");
        var bytes = Convert.FromBase64String(encoded);
        return Encoding.UTF8.GetString(bytes);
    }

    public Task<string> DescribeAsync(string path)
    {
        var quotedPath = ShellQuoting.Quote(path);
        return ExecuteAsync($"stat -c '%u %g %a' {quotedPath}");
    }

    public async Task<RemoteFileVersion> OpenVersionAsync(IRemoteEditorFileClient client, string path)
    {
        var opened = await Documents.ReadAsync(client, path, CancellationToken.None);
        opened.IsSuccess.Should().BeTrue(opened.Error?.Message);
        return opened.Value.Version;
    }

    public Task<EditorResult<RemoteSaveResult>> SaveAsync(IRemoteEditorFileClient client, string path, string text, RemoteFileVersion? expectedVersion)
    {
        var location = Locate(path);
        var content = Encoding.UTF8.GetBytes(text);
        var request = new RemoteSaveRequest(location, content, expectedVersion);
        return Saver.SaveAsync(client, request, CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        for (var i = _opened.Count - 1; i >= 0; i--)
        {
            var opened = _opened[i];
            await opened.DisposeAsync();
        }

        await _services.DisposeAsync();
    }

    private static SudoPasswordInput? CreateInput(string? sudoPassword)
    {
        if (sudoPassword is null)
        {
            return null;
        }

        using var secret = Secret.FromString(sudoPassword);
        return SudoPasswordInput.From(secret);
    }
}

internal readonly record struct ProtectedFile(string Directory, string Name, string Text)
{
    public string Owner { get; init; } = "root:root";

    public string Mode { get; init; } = "600";
}
