using DTech.Core.Files;
using DTech.Core.Operations;
using DTech.Core.Safety;
using DTech.Core.Tests.Safety.Support;
using DTech.Core.Transfers;
using DTech.Core.Transfers.Execution;
using DTech.Core.Transfers.Queue;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Transfers;

[TestClass]
public sealed class TransferEnqueuerTests
{
    private static readonly Guid ConnectionId = Guid.NewGuid();

    private ITransferStrategy _strategy = null!;
    private ITransferJournal _journal = null!;
    private ITransferNotifier _notifier = null!;
    private TransferQueue _queue = null!;
    private TransferEnqueuer _enqueuer = null!;
    private WriteSafetyFixture _safety = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _strategy = Substitute.For<ITransferStrategy>();
        _strategy.Direction.Returns(TransferDirection.Upload);
        _strategy.InspectAsync(Arg.Any<TransferContext>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var context = call.Arg<TransferContext>();
            var name = Path.GetFileName(context.Request.SourcePath);
            var inspection = new TransferInspection($"/srv/{name}", name.StartsWith("existing", StringComparison.Ordinal));
            return FileResult<TransferInspection>.Success(inspection);
        });
        var client = Substitute.For<IRemoteFileClient>();
        var opener = Substitute.For<ITransferConnectionOpener>();
        opener.OpenAsync(ConnectionId, Arg.Any<CancellationToken>()).Returns(FileResult<IRemoteFileClient>.Success(client));
        _journal = Substitute.For<ITransferJournal>();
        _journal.CreateAsync(Arg.Any<TransferRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Guid.NewGuid());
        _notifier = Substitute.For<ITransferNotifier>();
        _queue = new TransferQueue();
        var strategies = new TransferStrategySelector([_strategy]);
        _safety = new WriteSafetyFixture();
        _enqueuer = new TransferEnqueuer(_queue, opener, strategies, _journal, _notifier, _safety.Gate);
    }

    [TestMethod]
    public async Task EnqueueAsync_TargetsExistWithoutOverwrite_ReturnsConflictsAndQueuesNothing()
    {
        var batch = CreateBatch(false, "/home/me/existing.txt", "/home/me/new.txt");

        var result = await _enqueuer.EnqueueAsync(batch, TestContext.CancellationToken);

        result.Conflicts.Should().Equal("/srv/existing.txt");
        result.Transfers.Should().BeEmpty();
        _queue.List().Should().BeEmpty();
        await _journal.DidNotReceive().CreateAsync(Arg.Any<TransferRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task EnqueueAsync_OverwriteConfirmed_QueuesEveryItemWithJournalEntry()
    {
        var batch = CreateBatch(true, "/home/me/existing.txt", "/home/me/new.txt");

        var result = await _enqueuer.EnqueueAsync(batch, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Transfers.Should().HaveCount(2);
        result.Transfers.Should().OnlyContain(transfer => transfer.Status == OperationStatus.Queued);
        _queue.List().Should().HaveCount(2);
        await _journal.Received(2).CreateAsync(Arg.Any<TransferRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _notifier.Received(2).TransferChanged(Arg.Any<TransferSnapshot>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task EnqueueAsync_OverwriteWithoutConfirmationToken_IsRejectedWithoutConnecting()
    {
        var batch = CreateUnconfirmedBatch(true, "/home/me/existing.txt");

        var result = await _enqueuer.EnqueueAsync(batch, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.ConfirmationRequired);
        _queue.List().Should().BeEmpty();
        await _strategy.DidNotReceive().InspectAsync(Arg.Any<TransferContext>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task EnqueueAsync_UploadToReadOnlyServer_IsBlocked(bool overwrite)
    {
        _safety.MarkReadOnly(ConnectionId);
        var batch = CreateBatch(overwrite, "/home/me/new.txt");

        var result = await _enqueuer.EnqueueAsync(batch, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.ReadOnlyServer);
        _queue.List().Should().BeEmpty();
        await _journal.DidNotReceive().CreateAsync(Arg.Any<TransferRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task EnqueueAsync_DownloadFromReadOnlyServer_IsAllowed()
    {
        _strategy.Direction.Returns(TransferDirection.Download);
        var strategies = new TransferStrategySelector([_strategy]);
        var opener = Substitute.For<ITransferConnectionOpener>();
        var client = Substitute.For<IRemoteFileClient>();
        opener.OpenAsync(ConnectionId, Arg.Any<CancellationToken>()).Returns(FileResult<IRemoteFileClient>.Success(client));
        var enqueuer = new TransferEnqueuer(_queue, opener, strategies, _journal, _notifier, _safety.Gate);
        _safety.MarkReadOnly(ConnectionId);
        var batch = CreateBatch(false, "/srv/new.txt") with { Direction = TransferDirection.Download };

        var result = await enqueuer.EnqueueAsync(batch, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        _queue.List().Should().ContainSingle();
    }

    [TestMethod]
    public async Task EnqueueAsync_TargetAlreadyBeingTransferred_IsRejected()
    {
        var first = CreateBatch(false, "/home/me/new.txt");
        await _enqueuer.EnqueueAsync(first, TestContext.CancellationToken);
        var second = CreateBatch(true, "/home/me/new.txt");

        var result = await _enqueuer.EnqueueAsync(second, TestContext.CancellationToken);

        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be(FileErrorCode.AlreadyExists);
        _queue.List().Should().HaveCount(1);
    }

    [TestMethod]
    public async Task EnqueueAsync_EmptySelection_IsInvalid()
    {
        var batch = CreateBatch(false);

        var result = await _enqueuer.EnqueueAsync(batch, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.Invalid);
    }

    [TestMethod]
    public async Task EnqueueAsync_InspectionFails_ReturnsErrorAndQueuesNothing()
    {
        var missing = FileError.NotFound("/home/me/gone.txt");
        _strategy.InspectAsync(Arg.Any<TransferContext>(), Arg.Any<CancellationToken>()).Returns(FileResult<TransferInspection>.Failure(missing));
        var batch = CreateBatch(false, "/home/me/gone.txt");

        var result = await _enqueuer.EnqueueAsync(batch, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.NotFound);
        _queue.List().Should().BeEmpty();
    }

    [TestMethod]
    public async Task RetryAsync_ReplacesPreviousTransferWithResumingJob()
    {
        var batch = CreateBatch(false, "/home/me/new.txt");
        var queued = await _enqueuer.EnqueueAsync(batch, TestContext.CancellationToken);
        var previous = _queue.Find(queued.Transfers[0].Id)!;
        previous.RequestCancel();
        var request = previous.Request;
        var operation = new Operation
        {
            Id = previous.Id,
            Kind = TransferOperationKinds.Upload,
            Server = request.ServerName,
            Status = OperationStatus.Cancelled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Details = TransferOperationDetails.Describe(request, previous.TargetPath),
            Retryable = true,
        };

        var result = await _enqueuer.RetryAsync(operation, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        var jobs = _queue.List();
        jobs.Should().ContainSingle();
        jobs[0].Id.Should().NotBe(previous.Id);
        jobs[0].ResumeExisting.Should().BeTrue();
        jobs[0].Request.Should().Be(request);
        await _notifier.Received(1).TransferRemoved(previous.Id, Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task RetryAsync_UploadToReadOnlyServer_IsBlocked()
    {
        var batch = CreateBatch(false, "/home/me/new.txt");
        var queued = await _enqueuer.EnqueueAsync(batch, TestContext.CancellationToken);
        var previous = _queue.Find(queued.Transfers[0].Id)!;
        var operation = new Operation
        {
            Id = previous.Id,
            Kind = TransferOperationKinds.Upload,
            Server = previous.Request.ServerName,
            Status = OperationStatus.Failed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Details = TransferOperationDetails.Describe(previous.Request, previous.TargetPath),
            Retryable = true,
        };
        _safety.MarkReadOnly(ConnectionId);

        var result = await _enqueuer.RetryAsync(operation, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.ReadOnlyServer);
        _queue.List().Should().ContainSingle().Which.Id.Should().Be(previous.Id);
    }

    private TransferBatchRequest CreateBatch(bool overwrite, params string[] sources)
    {
        var batch = CreateUnconfirmedBatch(overwrite, sources);
        if (!overwrite)
        {
            return batch;
        }

        var token = _safety.Confirm(DangerousOperation.TransferOverwrite, ConnectionId, batch.TargetDirectory);
        return batch with { ConfirmationToken = token };
    }

    private static TransferBatchRequest CreateUnconfirmedBatch(bool overwrite, params string[] sources)
    {
        return new TransferBatchRequest
        {
            ConnectionId = ConnectionId,
            ServerName = "web-01",
            Direction = TransferDirection.Upload,
            SourcePaths = sources,
            TargetDirectory = "/srv",
            Overwrite = overwrite,
        };
    }
}
