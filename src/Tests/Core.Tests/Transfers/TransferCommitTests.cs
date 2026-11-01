using DTech.Core.Connections;
using DTech.Core.Files;
using DTech.Core.Files.Local;
using DTech.Core.Files.Remote;
using DTech.Core.Transfers;
using DTech.Core.Transfers.Execution;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Transfers;

[TestClass]
public sealed class TransferCommitTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task DownloadCommit_TargetAppearedWithoutOverwrite_PreservesTargetAndPartial()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"termforge-commit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var destination = Path.Combine(directory, "a.txt");
            var partial = Path.Combine(directory, ".a.part");
            await File.WriteAllTextAsync(destination, "new external data", TestContext.CancellationToken);
            await File.WriteAllTextAsync(partial, "copied data", TestContext.CancellationToken);
            var localPaths = new LocalPathResolver();
            var remotePaths = new RemotePathNormalizer();
            var planner = new RemoteTransferPlanner();
            var strategy = new DownloadTransferStrategy(localPaths, remotePaths, planner);
            var client = Substitute.For<IRemoteFileClient>();
            var request = CreateRequest(TransferDirection.Download, directory);
            var context = new TransferContext(client, request);
            var item = CreateItem(destination);
            var copy = new TransferCopy(item, partial, 0);

            var result = await strategy.CommitAsync(context, copy, TestContext.CancellationToken);

            result.IsSuccess.Should().BeFalse();
            var destinationContent = await File.ReadAllTextAsync(destination, TestContext.CancellationToken);
            var partialContent = await File.ReadAllTextAsync(partial, TestContext.CancellationToken);
            destinationContent.Should().Be("new external data");
            partialContent.Should().Be("copied data");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task UploadCommit_WithoutOverwrite_UsesNonReplacingRename()
    {
        var localPaths = new LocalPathResolver();
        var remotePaths = new RemotePathNormalizer();
        var planner = new LocalTransferPlanner();
        var strategy = new UploadTransferStrategy(localPaths, remotePaths, planner);
        var client = Substitute.For<IRemoteFileClient>();
        var success = SshResult.Success();
        client.RenameAsync(Arg.Any<RemoteRenameRequest>(), Arg.Any<CancellationToken>()).Returns(success);
        var request = CreateRequest(TransferDirection.Upload, "/srv");
        var context = new TransferContext(client, request);
        var item = CreateItem("/srv/a.txt");
        var copy = new TransferCopy(item, "/srv/.a.part", 0);

        var result = await strategy.CommitAsync(context, copy, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        await client.Received(1).RenameAsync(Arg.Any<RemoteRenameRequest>(), Arg.Any<CancellationToken>());
        await client.DidNotReceive().ReplaceAsync(Arg.Any<RemoteRenameRequest>(), Arg.Any<CancellationToken>());
    }

    private static TransferRequest CreateRequest(TransferDirection direction, string targetDirectory)
    {
        return new TransferRequest
        {
            ConnectionId = Guid.NewGuid(),
            ServerName = "web-01",
            Direction = direction,
            SourcePath = "/source/a.txt",
            TargetDirectory = targetDirectory,
        };
    }

    private static TransferItem CreateItem(string target)
    {
        return new TransferItem("/source/a.txt", target, false)
        {
            TargetName = "a.txt",
            Size = 11,
            ModifiedUtc = DateTime.UtcNow,
        };
    }
}
