using DTech.Core.Files;
using DTech.Core.Tests.Safety.Support;
using DTech.Core.Transfers;
using DTech.Core.Transfers.Execution;
using DTech.Core.Transfers.Queue;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Transfers;

[TestClass]
public sealed class RemoteTransferAdmissionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Enqueue_ReadOnlySource_IsAllowed()
    {
        var fixture = new Fixture();
        fixture.Safety.MarkReadOnly(fixture.Batch.ConnectionId);

        var result = await fixture.Enqueuer.EnqueueAsync(fixture.Batch, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Transfers[0].Request.TargetConnectionId.Should().Be(fixture.Batch.TargetConnectionId);
    }

    [TestMethod]
    public async Task Enqueue_ReadOnlyTarget_IsRejectedBeforeConnecting()
    {
        var fixture = new Fixture();
        fixture.Safety.MarkReadOnly(fixture.Batch.TargetConnectionId!.Value);

        var result = await fixture.Enqueuer.EnqueueAsync(fixture.Batch, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(FileErrorCode.ReadOnlyServer);
        await fixture.Opener.DidNotReceive().OpenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Enqueue_Overwrite_ConsumesDestinationConfirmation()
    {
        var fixture = new Fixture();
        var token = fixture.Safety.Confirm(DTech.Core.Safety.DangerousOperation.TransferOverwrite, fixture.Batch.TargetConnectionId!.Value, "/srv");
        var batch = fixture.Batch with { Overwrite = true, ConfirmationToken = token };

        var result = await fixture.Enqueuer.EnqueueAsync(batch, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue();
    }

    [TestMethod]
    public void Recovery_SourceVersionChange_IsRejected()
    {
        var fixture = new Fixture();
        var request = new TransferRequest
        {
            ConnectionId = fixture.Batch.ConnectionId, TargetConnectionId = fixture.Batch.TargetConnectionId,
            ServerName = "source", Direction = TransferDirection.RemoteToRemote, SourcePath = "/a", TargetDirectory = "/srv",
        };
        var job = new TransferJob(Guid.NewGuid(), request, "/srv/a") { ResumeExisting = true };
        job.RestoreSourceVersions([new TransferSourceVersion("/a", 10, DateTime.UnixEpoch)]);
        var item = new TransferItem("/a", "/srv/a", false) { Size = 10, ModifiedUtc = DateTime.UnixEpoch.AddSeconds(1) };

        job.CheckSourceVersions([item]).Should().BeFalse();
        job.FinishProcessing();
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            var strategy = Substitute.For<ITransferStrategy>();
            strategy.Direction.Returns(TransferDirection.RemoteToRemote);
            var inspection = new TransferInspection("/srv/a", false);
            strategy.InspectAsync(Arg.Any<TransferContext>(), Arg.Any<CancellationToken>()).Returns(FileResult<TransferInspection>.Success(inspection));
            var client = Substitute.For<IRemoteFileClient>();
            Opener.OpenAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(FileResult<IRemoteFileClient>.Success(client));
            var journal = Substitute.For<ITransferJournal>();
            journal.CreateAsync(Arg.Any<TransferRequest>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => Guid.NewGuid());
            var queue = new TransferQueue();
            var strategies = new TransferStrategySelector([strategy]);
            var notifier = Substitute.For<ITransferNotifier>();
            Enqueuer = new TransferEnqueuer(queue, Opener, strategies, journal, notifier, Safety.Gate);
        }
        public WriteSafetyFixture Safety { get; } = new();
        public ITransferConnectionOpener Opener { get; } = Substitute.For<ITransferConnectionOpener>();
        public TransferEnqueuer Enqueuer { get; }
        public TransferBatchRequest Batch { get; } = new()
        {
            ConnectionId = Guid.NewGuid(), TargetConnectionId = Guid.NewGuid(), ServerName = "source", TargetServerName = "target",
            Direction = TransferDirection.RemoteToRemote, SourcePaths = ["/a"], TargetDirectory = "/srv",
        };
    }
}
