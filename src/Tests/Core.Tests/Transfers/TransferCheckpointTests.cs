using DTech.Core.Files;
using DTech.Core.Transfers;
using DTech.Core.Transfers.Execution;
using DTech.Core.Transfers.Queue;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Transfers;

[TestClass]
public sealed class TransferCheckpointTests
{
    private ITransferStrategy _strategy = null!;
    private ITransferJournal _journal = null!;
    private TransferFileContext _file = null!;
    private TransferItem _item = null!;

    [TestInitialize]
    public void Initialize()
    {
        var request = new TransferRequest
        {
            ConnectionId = Guid.NewGuid(),
            ServerName = "server",
            Direction = TransferDirection.Download,
            SourcePath = "/remote/file",
            TargetDirectory = "/local",
        };
        var id = Guid.NewGuid();
        var job = new TransferJob(id, request, "/local/file");
        var client = Substitute.For<IRemoteFileClient>();
        var context = new TransferContext(client, request);
        _strategy = Substitute.For<ITransferStrategy>();
        _journal = Substitute.For<ITransferJournal>();
        _file = new TransferFileContext(_strategy, context, job);
        _item = new TransferItem("/remote/file", "/local/file", false) { Size = 100, TargetName = "file" };
        var absent = FileResult<long?>.Success(null);
        var success = FileResult.Success();
        _strategy.FindTargetSizeAsync(context, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(absent);
        _strategy.GetPartialPath(_item, Arg.Any<string>()).Returns("/local/.file.part");
        _strategy.CopyAsync(context, Arg.Any<TransferCopy>(), Arg.Any<CancellationToken>()).Returns(success);
        _strategy.CommitAsync(context, Arg.Any<TransferCopy>(), Arg.Any<CancellationToken>()).Returns(success);
    }

    [TestMethod]
    public async Task Copy_CheckpointsExactPartialBeforeWritingAndClearsItAfterCommit()
    {
        var events = new List<string>();
        _journal.CheckpointAsync(_file.Job, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            var recovery = _file.Job.CaptureRecovery();
            events.Add(recovery.PartialPaths.Count == 0 ? "cleared" : recovery.PartialPaths[0]);
            return Task.CompletedTask;
        });
        _strategy.CopyAsync(_file.Context, Arg.Any<TransferCopy>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            events.Add("copy");
            return FileResult.Success();
        });
        _strategy.CommitAsync(_file.Context, Arg.Any<TransferCopy>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            events.Add("commit");
            return FileResult.Success();
        });
        var copier = new TransferFileCopier(_journal);

        var result = await copier.CopyAsync(_file, _item, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        events.Should().Equal("/local/.file.part", "copy", "commit", "cleared");
    }

    [TestMethod]
    public async Task Copy_FailedCheckpoint_DoesNotCreateOrCommitPartial()
    {
        _journal.CheckpointAsync(_file.Job, Arg.Any<CancellationToken>()).Returns(_ => throw new IOException("database unavailable"));
        var copier = new TransferFileCopier(_journal);
        var copying = () => copier.CopyAsync(_file, _item, CancellationToken.None);

        await copying.Should().ThrowAsync<IOException>().WithMessage("database unavailable");

        await _strategy.DidNotReceive().CopyAsync(_file.Context, Arg.Any<TransferCopy>(), Arg.Any<CancellationToken>());
        await _strategy.DidNotReceive().CommitAsync(_file.Context, Arg.Any<TransferCopy>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Copy_FailedCommit_KeepsCheckpointForCleanup()
    {
        var error = new FileError(FileErrorCode.ConnectionFailed, "disconnected");
        var failure = FileResult.Failure(error);
        _strategy.CommitAsync(_file.Context, Arg.Any<TransferCopy>(), Arg.Any<CancellationToken>()).Returns(failure);
        var copier = new TransferFileCopier(_journal);

        var result = await copier.CopyAsync(_file, _item, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _file.Job.CaptureRecovery().PartialPaths.Should().Equal("/local/.file.part");
        await _journal.Received(1).CheckpointAsync(_file.Job, CancellationToken.None);
    }
}
