using System.Security.Cryptography;
using DTech.Core.Connections;
using DTech.Core.Files;
using DTech.Core.Files.Local;
using DTech.Core.Files.Remote;
using DTech.Core.Safety;
using DTech.Core.Transfers;
using DTech.Core.Transfers.Execution;
using DTech.Core.Transfers.Queue;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace DTech.Ssh.IntegrationTests.Transfers;

internal sealed class TransferHarness : IAsyncDisposable
{
    public static readonly Guid ConnectionId = Guid.NewGuid();

    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    private readonly ServiceProvider _services;

    private TransferHarness(ServiceProvider services, IRemoteFileClient inspector, string remoteDirectory)
    {
        _services = services;
        Inspector = inspector;
        RemoteDirectory = remoteDirectory;
        LocalDirectory = Path.Combine(Path.GetTempPath(), $"termforge-transfer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(LocalDirectory);
        var factory = services.GetRequiredService<IRemoteFileClientFactory>();
        var opener = new InterceptingOpener(factory, this);
        var localPaths = new LocalPathResolver();
        var remotePaths = new RemotePathNormalizer();
        var upload = new UploadTransferStrategy(localPaths, remotePaths, new LocalTransferPlanner());
        var download = new DownloadTransferStrategy(localPaths, remotePaths, new RemoteTransferPlanner());
        var strategies = new TransferStrategySelector([upload, download]);
        var executor = new TransferExecutor(opener, strategies, new TransferFileCopier());
        var options = new TransferOptions { ProgressIntervalMilliseconds = 50 };
        var ticker = new TransferProgressTicker(TimeProvider.System, options, Notifier);
        Runner = new TransferRunner(executor, ticker, Journal, Notifier);
        var accessModes = Substitute.For<IServerAccessModeReader>();
        var writeGate = new ServerWriteGate(accessModes, Confirmations);
        Enqueuer = new TransferEnqueuer(Queue, opener, strategies, Journal, Notifier, writeGate);
        Control = new TransferControl(Queue, Journal, Notifier, opener, strategies);
    }

    public ConfirmationTokenStore Confirmations { get; } = new(TimeProvider.System);

    public IRemoteFileClient Inspector { get; }

    public string RemoteDirectory { get; }

    public string LocalDirectory { get; }

    public SshConnectionTarget Target { get; private init; } = null!;

    public ClientInterception Interception { get; set; } = new();

    public RecordingTransferJournal Journal { get; } = new();

    public RecordingTransferNotifier Notifier { get; } = new();

    public TransferQueue Queue { get; } = new();

    public TransferRunner Runner { get; }

    public TransferEnqueuer Enqueuer { get; }

    public TransferControl Control { get; }

    public static async Task<TransferHarness> OpenAsync()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<IRemoteFileClientFactory>();
        var target = cluster.Target.CreateKeyTarget();
        var connected = await factory.ConnectAsync(target, CancellationToken.None);
        connected.IsSuccess.Should().BeTrue(connected.Error?.Message);
        var inspector = connected.Value;
        var remoteDirectory = $"{SshServerContainer.HomeDirectory}/transfer-{Guid.NewGuid():N}";
        var created = await inspector.CreateDirectoryAsync(remoteDirectory, CancellationToken.None);
        created.IsSuccess.Should().BeTrue(created.Error?.Message);
        return new TransferHarness(services, inspector, remoteDirectory) { Target = target };
    }

    public async Task<TransferJob> EnqueueAsync(TransferDirection direction, string sourcePath, string targetDirectory)
    {
        var result = await EnqueueBatchAsync(direction, sourcePath, targetDirectory, false);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        return Queue.Find(result.Transfers[0].Id)!;
    }

    public Task<TransferEnqueueResult> EnqueueBatchAsync(TransferDirection direction, string sourcePath, string targetDirectory, bool overwrite)
    {
        var batch = new TransferBatchRequest
        {
            ConnectionId = ConnectionId,
            ServerName = "ssh-target",
            Direction = direction,
            SourcePaths = [sourcePath],
            TargetDirectory = targetDirectory,
            Overwrite = overwrite,
        };
        if (overwrite && direction == TransferDirection.Upload)
        {
            var scope = new ConfirmationScope(DangerousOperation.TransferOverwrite, ConnectionId, targetDirectory);
            var grant = Confirmations.Issue(scope);
            batch = batch with { ConfirmationToken = grant.Token };
        }

        return Enqueuer.EnqueueAsync(batch, CancellationToken.None);
    }

    public string CreateLocalFile(string relativePath, int size)
    {
        var path = Path.Combine(LocalDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var content = new byte[size];
        Random.Shared.NextBytes(content);
        File.WriteAllBytes(path, content);
        return path;
    }

    public async Task<string> CreateRemoteFileAsync(string relativePath, int size)
    {
        var path = $"{RemoteDirectory}/{relativePath}";
        var content = new byte[size];
        Random.Shared.NextBytes(content);
        using var source = new MemoryStream(content);
        var uploaded = await Inspector.UploadAsync(source, path, CancellationToken.None);
        uploaded.IsSuccess.Should().BeTrue(uploaded.Error?.Message);
        return path;
    }

    public async Task CreateRemoteDirectoryAsync(string relativePath)
    {
        var created = await Inspector.CreateDirectoryAsync($"{RemoteDirectory}/{relativePath}", CancellationToken.None);
        created.IsSuccess.Should().BeTrue(created.Error?.Message);
    }

    public async Task<string> RemoteHashAsync(string remotePath)
    {
        using var content = new MemoryStream();
        var downloaded = await Inspector.DownloadAsync(remotePath, content, CancellationToken.None);
        downloaded.IsSuccess.Should().BeTrue(downloaded.Error?.Message);
        var hash = SHA256.HashData(content.ToArray());
        return Convert.ToHexString(hash);
    }

    public async Task<IReadOnlyList<RemoteFileEntry>> ListRemoteAsync(string remotePath)
    {
        var listed = await Inspector.ListAsync(remotePath, CancellationToken.None);
        listed.IsSuccess.Should().BeTrue(listed.Error?.Message);
        return listed.Value;
    }

    public static string LocalHash(string localPath)
    {
        var bytes = File.ReadAllBytes(localPath);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + WaitTimeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail("Condition was not met in time.");
            }

            await Task.Delay(10);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Inspector.DisposeAsync();
        await _services.DisposeAsync();
        if (Directory.Exists(LocalDirectory))
        {
            Directory.Delete(LocalDirectory, true);
        }
    }

    private sealed class InterceptingOpener(IRemoteFileClientFactory factory, TransferHarness harness) : ITransferConnectionOpener
    {
        public async Task<FileResult<IRemoteFileClient>> OpenAsync(Guid connectionId, CancellationToken cancellationToken)
        {
            var connected = await factory.ConnectAsync(harness.Target, cancellationToken);
            if (!connected.IsSuccess)
            {
                var error = FileErrorMapper.FromSsh(connected.Error!, harness.Target.Endpoint.Host);
                return FileResult<IRemoteFileClient>.Failure(error);
            }

            var client = new InterceptingRemoteFileClient(connected.Value, harness.Interception);
            return FileResult<IRemoteFileClient>.Success(client);
        }
    }
}
