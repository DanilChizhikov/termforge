using DTech.Core.Files;
using DTech.Core.Operations;
using DTech.Core.Transfers;
using DTech.Core.Transfers.Execution;
using DTech.Core.Transfers.Queue;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Transfers;

[TestClass]
public sealed class TransferExecutorTests
{
    private static readonly Guid ConnectionId = Guid.NewGuid();

    private ITransferStrategy _strategy = null!;
    private ITransferConnectionOpener _opener = null!;
    private TransferExecutor _executor = null!;

    [TestInitialize]
    public void Initialize()
    {
        _strategy = Substitute.For<ITransferStrategy>();
        _strategy.Direction.Returns(TransferDirection.Download);
        _strategy.GetPartialPath(Arg.Any<TransferItem>(), Arg.Any<string>()).Returns(call => $"/local/{call.ArgAt<string>(1)}");
        _strategy.FindTargetSizeAsync(Arg.Any<TransferContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FileResult<long?>.Success(null));
        _strategy.CreateDirectoryAsync(Arg.Any<TransferContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FileResult.Success());
        _strategy.CommitAsync(Arg.Any<TransferContext>(), Arg.Any<TransferCopy>(), Arg.Any<CancellationToken>()).Returns(FileResult.Success());
        _strategy.DeletePartialAsync(Arg.Any<TransferContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(FileResult.Success());
        var client = Substitute.For<IRemoteFileClient>();
        _opener = Substitute.For<ITransferConnectionOpener>();
        _opener.OpenAsync(ConnectionId, Arg.Any<CancellationToken>()).Returns(FileResult<IRemoteFileClient>.Success(client));
        var strategies = new TransferStrategySelector([_strategy]);
        _executor = new TransferExecutor(_opener, strategies, new TransferFileCopier());
    }

    [TestMethod]
    public async Task RunAsync_DirectoryPlan_CreatesDirectoriesBeforeFilesAndCommitsEach()
    {
        var plan = CreatePlan(DirectoryItem("/local/site"), FileItem("/local/site/a.txt", 10), FileItem("/local/site/b.txt", 20));
        GivenPlan(plan);
        GivenCopySucceeds();
        var job = CreateStartedJob(overwrite: false);

        var outcome = await _executor.RunAsync(job);

        outcome.Status.Should().Be(OperationStatus.Succeeded);
        Received.InOrder(() =>
        {
            _strategy.CreateDirectoryAsync(Arg.Any<TransferContext>(), "/local/site", Arg.Any<CancellationToken>());
            _strategy.CommitAsync(Arg.Any<TransferContext>(), Arg.Is<TransferCopy>(copy => copy.Item.TargetPath == "/local/site/a.txt"), Arg.Any<CancellationToken>());
            _strategy.CommitAsync(Arg.Any<TransferContext>(), Arg.Is<TransferCopy>(copy => copy.Item.TargetPath == "/local/site/b.txt"), Arg.Any<CancellationToken>());
        });
        var progress = job.Progress();
        progress.TransferredBytes.Should().Be(30);
        progress.CompletedFiles.Should().Be(2);
    }

    [TestMethod]
    public async Task RunAsync_CopiesIntoPartialAndNeverWritesTargetDirectly()
    {
        GivenPlan(CreatePlan(FileItem("/local/a.txt", 10)));
        GivenCopySucceeds();
        var job = CreateStartedJob(overwrite: false);

        await _executor.RunAsync(job);

        await _strategy.Received(1).CopyAsync(
            Arg.Any<TransferContext>(),
            Arg.Is<TransferCopy>(copy => copy.PartialPath != copy.Item.TargetPath && copy.PartialPath.EndsWith(TransferPartialName.Suffix)),
            Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task RunAsync_ExistingPartial_ResumesFromItsSize()
    {
        GivenPlan(CreatePlan(FileItem("/local/a.txt", 100)));
        GivenCopySucceeds();
        _strategy.FindTargetSizeAsync(Arg.Any<TransferContext>(), Arg.Is<string>(path => path.EndsWith(TransferPartialName.Suffix)), Arg.Any<CancellationToken>())
            .Returns(FileResult<long?>.Success(40));
        var job = CreateStartedJob(overwrite: false);

        await _executor.RunAsync(job);

        await _strategy.Received(1).CopyAsync(Arg.Any<TransferContext>(), Arg.Is<TransferCopy>(copy => copy.Offset == 40), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task RunAsync_PartialLargerThanSource_StartsOver()
    {
        GivenPlan(CreatePlan(FileItem("/local/a.txt", 100)));
        GivenCopySucceeds();
        _strategy.FindTargetSizeAsync(Arg.Any<TransferContext>(), Arg.Is<string>(path => path.EndsWith(TransferPartialName.Suffix)), Arg.Any<CancellationToken>())
            .Returns(FileResult<long?>.Success(500));
        var job = CreateStartedJob(overwrite: false);

        await _executor.RunAsync(job);

        await _strategy.Received(1).CopyAsync(Arg.Any<TransferContext>(), Arg.Is<TransferCopy>(copy => copy.Offset == 0), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task RunAsync_TargetAppearedWithoutOverwrite_FailsWithoutCopying()
    {
        GivenPlan(CreatePlan(FileItem("/local/a.txt", 10)));
        _strategy.FindTargetSizeAsync(Arg.Any<TransferContext>(), "/local/a.txt", Arg.Any<CancellationToken>()).Returns(FileResult<long?>.Success(3));
        var job = CreateStartedJob(overwrite: false);

        var outcome = await _executor.RunAsync(job);

        outcome.Status.Should().Be(OperationStatus.Failed);
        outcome.Failure!.Retryable.Should().BeFalse();
        await _strategy.DidNotReceive().CopyAsync(Arg.Any<TransferContext>(), Arg.Any<TransferCopy>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task RunAsync_ResumeSkipsFilesAlreadyCompletedByPreviousAttempt()
    {
        GivenPlan(CreatePlan(FileItem("/local/a.txt", 10)));
        _strategy.FindTargetSizeAsync(Arg.Any<TransferContext>(), "/local/a.txt", Arg.Any<CancellationToken>()).Returns(FileResult<long?>.Success(10));
        var job = CreateStartedJob(overwrite: false, resume: true);

        var outcome = await _executor.RunAsync(job);

        outcome.Status.Should().Be(OperationStatus.Succeeded);
        await _strategy.DidNotReceive().CopyAsync(Arg.Any<TransferContext>(), Arg.Any<TransferCopy>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task RunAsync_RetryableFailure_KeepsPartialForResume()
    {
        GivenPlan(CreatePlan(FileItem("/local/a.txt", 10)));
        GivenCopyFails(FileErrorCode.ConnectionFailed);
        var job = CreateStartedJob(overwrite: false);

        var outcome = await _executor.RunAsync(job);

        outcome.Status.Should().Be(OperationStatus.Failed);
        outcome.Failure!.Retryable.Should().BeTrue();
        outcome.PartialPath.Should().EndWith(TransferPartialName.Suffix);
        await _strategy.DidNotReceive().DeletePartialAsync(Arg.Any<TransferContext>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task RunAsync_PermanentFailure_DeletesPartial()
    {
        GivenPlan(CreatePlan(FileItem("/local/a.txt", 10)));
        GivenCopyFails(FileErrorCode.PermissionDenied);
        var job = CreateStartedJob(overwrite: false);

        var outcome = await _executor.RunAsync(job);

        outcome.Status.Should().Be(OperationStatus.Failed);
        outcome.Failure!.Retryable.Should().BeFalse();
        outcome.PartialPath.Should().BeNull();
        await _strategy.Received(1).DeletePartialAsync(Arg.Any<TransferContext>(), Arg.Is<string>(path => path.EndsWith(TransferPartialName.Suffix)), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task RunAsync_CancelledDuringCopy_DeletesPartialAndReportsCancelled()
    {
        GivenPlan(CreatePlan(FileItem("/local/a.txt", 10)));
        var job = CreateStartedJob(overwrite: false);
        _strategy.CopyAsync(Arg.Any<TransferContext>(), Arg.Any<TransferCopy>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            job.RequestCancel();
            var cancelled = new FileError(FileErrorCode.Cancelled, "cancelled");
            return FileResult.Failure(cancelled);
        });

        var outcome = await _executor.RunAsync(job);

        outcome.Status.Should().Be(OperationStatus.Cancelled);
        await _strategy.Received(1).DeletePartialAsync(Arg.Any<TransferContext>(), Arg.Is<string>(path => path.EndsWith(TransferPartialName.Suffix)), CancellationToken.None);
        await _strategy.DidNotReceive().CommitAsync(Arg.Any<TransferContext>(), Arg.Any<TransferCopy>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task RunAsync_ConnectionUnavailable_FailsRetryably()
    {
        var unreachable = new FileError(FileErrorCode.ConnectionFailed, "unreachable");
        _opener.OpenAsync(ConnectionId, Arg.Any<CancellationToken>()).Returns(FileResult<IRemoteFileClient>.Failure(unreachable));
        var job = CreateStartedJob(overwrite: false);

        var outcome = await _executor.RunAsync(job);

        outcome.Status.Should().Be(OperationStatus.Failed);
        outcome.Failure!.Retryable.Should().BeTrue();
        outcome.Failure.Error.Should().Be("unreachable");
    }

    private void GivenPlan(TransferPlan plan)
    {
        _strategy.PlanAsync(Arg.Any<TransferContext>(), Arg.Any<CancellationToken>()).Returns(FileResult<TransferPlan>.Success(plan));
    }

    private void GivenCopySucceeds()
    {
        _strategy.CopyAsync(Arg.Any<TransferContext>(), Arg.Any<TransferCopy>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var copy = call.Arg<TransferCopy>();
            copy.Progress?.Report(copy.Item.Size - copy.Offset);
            return FileResult.Success();
        });
    }

    private void GivenCopyFails(FileErrorCode code)
    {
        var error = new FileError(code, "copy failed");
        _strategy.CopyAsync(Arg.Any<TransferContext>(), Arg.Any<TransferCopy>(), Arg.Any<CancellationToken>()).Returns(FileResult.Failure(error));
    }

    private static TransferPlan CreatePlan(params TransferItem[] items)
    {
        return new TransferPlan(items);
    }

    private static TransferItem DirectoryItem(string target)
    {
        return new TransferItem($"/remote{target}", target, true) { TargetName = Path.GetFileName(target) };
    }

    private static TransferItem FileItem(string target, long size)
    {
        return new TransferItem($"/remote{target}", target, false)
        {
            Size = size,
            ModifiedUtc = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc),
            TargetName = Path.GetFileName(target),
        };
    }

    private static TransferJob CreateStartedJob(bool overwrite, bool resume = false)
    {
        var request = new TransferRequest
        {
            ConnectionId = ConnectionId,
            ServerName = "web-01",
            Direction = TransferDirection.Download,
            SourcePath = "/remote/a.txt",
            TargetDirectory = "/local",
            Overwrite = overwrite,
        };
        var job = new TransferJob(Guid.NewGuid(), request, "/local/a.txt") { ResumeExisting = resume };
        job.TryStart();
        return job;
    }
}
