using System.Security.Cryptography;
using DTech.Core.Files;
using DTech.Core.Files.Local;
using DTech.Core.Files.Remote;
using DTech.Core.Operations;
using DTech.Core.Safety;
using DTech.Core.Transfers;
using DTech.Core.Transfers.Execution;
using DTech.Core.Transfers.Queue;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace DTech.Ssh.IntegrationTests.Transfers;

[TestClass]
public sealed class RemoteTransferTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Copy_BothDirections_StreamsLargeFilesAndDirectoryTrees()
    {
        await using var harness = await OpenAsync();
        var directory = $"{SshServerContainer.HomeDirectory}/copy-{Guid.NewGuid():N}";
        var nested = directory + "/nested";
        await harness.Source.CreateDirectoryAsync(directory, TestContext.CancellationToken);
        await harness.Source.CreateDirectoryAsync(nested, TestContext.CancellationToken);
        var bytes = RandomNumberGenerator.GetBytes(3 * 1024 * 1024);
        using var content = new MemoryStream(bytes);
        await harness.Source.UploadAsync(content, nested + "/large.bin", TestContext.CancellationToken);
        var first = harness.Job(directory, SshServerContainer.HomeDirectory);

        var result = await harness.Executor.RunAsync(first);

        result.Status.Should().Be(OperationStatus.Succeeded);
        using var copied = new MemoryStream();
        await harness.Target.DownloadAsync(nested + "/large.bin", copied, TestContext.CancellationToken);
        SHA256.HashData(copied.ToArray()).Should().Equal(SHA256.HashData(bytes));
        var reverseDirectory = directory + "-reverse";
        await harness.Source.CreateDirectoryAsync(reverseDirectory, TestContext.CancellationToken);
        var reversed = harness.Job(nested + "/large.bin", reverseDirectory, reverse: true);
        var reverseResult = await harness.Executor.RunAsync(reversed);
        reverseResult.Status.Should().Be(OperationStatus.Succeeded);
        using var returned = new MemoryStream();
        await harness.Source.DownloadAsync(reverseDirectory + "/large.bin", returned, TestContext.CancellationToken);
        returned.ToArray().Should().Equal(bytes);
    }

    [TestMethod]
    public async Task Verify_ChangedPartialOfTheSameSize_IsRejected()
    {
        await using var harness = await OpenAsync();
        var source = $"{SshServerContainer.HomeDirectory}/source-{Guid.NewGuid():N}";
        var target = $"{SshServerContainer.HomeDirectory}/partial-{Guid.NewGuid():N}";
        using var original = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        using var changed = new MemoryStream(new byte[] { 4, 3 });
        await harness.Source.UploadAsync(original, source, TestContext.CancellationToken);
        await harness.Target.UploadAsync(changed, target, TestContext.CancellationToken);
        var stat = await harness.Source.StatAsync(source, TestContext.CancellationToken);
        var item = new TransferItem(source, target, false) { Size = 4, ModifiedUtc = stat.Value.LastWriteTimeUtc };
        var request = harness.Job(source, SshServerContainer.HomeDirectory).Request;
        var context = new TransferContext(harness.Source, request) { TargetClient = harness.Target };
        var verifier = new RemoteTransferVerifier();
        var copy = new TransferCopy(item, target, 2);

        var result = await verifier.VerifyAsync(context, copy, TestContext.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Message.Should().Contain("Start the transfer again");
    }

    [TestMethod]
    public async Task Copy_ReadOnlyDestination_IsBlockedBeforeWriting()
    {
        await using var harness = await OpenAsync();
        harness.Access.IsReadOnlyAsync(harness.TargetId, Arg.Any<CancellationToken>()).Returns(true);
        var job = harness.Job("/missing", "/tmp");
        var context = new TransferContext(harness.Source, job.Request) { TargetClient = harness.Target };

        var result = await harness.Strategy.CreateDirectoryAsync(context, "/tmp/must-not-exist", TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.ReadOnlyServer);
    }

    private static async Task<Harness> OpenAsync()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey, cluster.Jump.Identity.HostKey);
        var services = SshTransportTestServices.Create(verifier);
        var factory = services.GetRequiredService<IRemoteFileClientFactory>();
        var sourceTarget = cluster.Target.CreateKeyTarget();
        var destinationTarget = cluster.Jump.CreateKeyTarget();
        var source = await factory.ConnectAsync(sourceTarget, CancellationToken.None);
        var destination = await factory.ConnectAsync(destinationTarget, CancellationToken.None);
        source.IsSuccess.Should().BeTrue();
        destination.IsSuccess.Should().BeTrue();
        var harness = new Harness(services, source.Value, destination.Value);
        harness.Opener.OpenAsync(harness.SourceId, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var connected = await factory.ConnectAsync(sourceTarget, call.Arg<CancellationToken>());
            return FileResult<IRemoteFileClient>.Success(connected.Value);
        });
        harness.Opener.OpenAsync(harness.TargetId, Arg.Any<CancellationToken>()).Returns(async call =>
        {
            var connected = await factory.ConnectAsync(destinationTarget, call.Arg<CancellationToken>());
            return FileResult<IRemoteFileClient>.Success(connected.Value);
        });
        return harness;
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        public Harness(ServiceProvider services, IRemoteFileClient source, IRemoteFileClient target)
        {
            _services = services;
            Source = source;
            Target = target;
            var paths = new RemotePathNormalizer();
            var upload = new UploadTransferStrategy(new LocalPathResolver(), paths, new LocalTransferPlanner());
            var gate = new ServerWriteGate(Access, new ConfirmationTokenStore(TimeProvider.System));
            Strategy = new RemoteToRemoteTransferStrategy(paths, new RemoteTransferPlanner(), upload, gate, new RemoteTransferVerifier());
            var selector = new TransferStrategySelector([Strategy]);
            Executor = new TransferExecutor(Opener, selector, new TransferFileCopier());
        }
        public Guid SourceId { get; } = Guid.NewGuid();
        public Guid TargetId { get; } = Guid.NewGuid();
        public IRemoteFileClient Source { get; }
        public IRemoteFileClient Target { get; }
        public IServerAccessModeReader Access { get; } = Substitute.For<IServerAccessModeReader>();
        public ITransferConnectionOpener Opener { get; } = Substitute.For<ITransferConnectionOpener>();
        public RemoteToRemoteTransferStrategy Strategy { get; }
        public TransferExecutor Executor { get; }
        public TransferJob Job(string source, string directory, bool reverse = false)
        {
            var request = new TransferRequest
            {
                ConnectionId = reverse ? TargetId : SourceId,
                TargetConnectionId = reverse ? SourceId : TargetId,
                ServerName = "source", TargetServerName = "target", Direction = TransferDirection.RemoteToRemote,
                SourcePath = source, TargetDirectory = directory,
            };
            return new TransferJob(Guid.NewGuid(), request, directory + "/" + Path.GetFileName(source));
        }
        public async ValueTask DisposeAsync()
        {
            await Source.DisposeAsync();
            await Target.DisposeAsync();
            await _services.DisposeAsync();
        }
    }
}
