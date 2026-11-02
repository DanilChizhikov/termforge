using System.Text.Json;
using DTech.Core.Files;
using DTech.Core.Operations;
using DTech.Core.Transfers;
using DTech.Core.Transfers.Execution;
using DTech.Core.Transfers.Queue;
using DTech.Core.Tests.SavedConnections.Support;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Transfers;

[TestClass]
public sealed class TransferRecoveryTests
{
    [TestMethod]
    public void Restore_PartialsAreNotLostWhenAnotherFileCompletes()
    {
        var request = Request();
        var id = Guid.NewGuid();
        var job = new TransferJob(id, request, "/tmp/target");
        job.RestorePartials(["/tmp/.old.part"]);

        job.BeginFile("/tmp/.new.part", 0);
        job.CompleteFile(4);
        job.Succeed();

        var recovery = job.CaptureRecovery();
        recovery.PartialPaths.Should().Equal("/tmp/.old.part");
        job.Snapshot().HasPartial.Should().BeTrue();
    }

    [TestMethod]
    public void RestoreFinished_DoesNotQueueAWorkerAndDisposesCancellation()
    {
        var queue = new TransferQueue();
        var request = Request();
        var id = Guid.NewGuid();
        var job = new TransferJob(id, request, "/tmp/target");
        var progress = new TransferProgress(0, 0, 0, 0, 0, null);
        var failure = new TransferFailure("interrupted", true, progress);
        job.RestorePartials(["/tmp/.old.part"]);
        job.Fail(failure, null);

        queue.RestoreFinished(job);

        queue.Find(job.Id).Should().BeSameAs(job);
        queue.Pending.TryRead(out _).Should().BeFalse();
        job.ProcessingFinished.Should().BeTrue();
        job.RequestCancel().Should().Be(TransferCancelOutcome.AlreadyFinished);
    }

    [TestMethod]
    public async Task Discard_RestoredLocalPartials_WorksWhenServerIsOffline()
    {
        using var directory = new TemporaryDirectory();
        var first = directory.Write(".first.part", "partial one");
        var second = directory.Write(".second.part", "partial two");
        var request = Request();
        var id = Guid.NewGuid();
        var target = directory.Combine("target");
        var job = new TransferJob(id, request, target);
        var progress = new TransferProgress(0, 0, 0, 0, 0, null);
        var failure = new TransferFailure("interrupted", true, progress);
        job.RestorePartials([first, second]);
        job.Fail(failure, null);
        var queue = new TransferQueue();
        queue.RestoreFinished(job);
        var journal = Substitute.For<ITransferJournal>();
        var notifier = Substitute.For<ITransferNotifier>();
        var opener = Substitute.For<ITransferConnectionOpener>();
        var strategies = new TransferStrategySelector([]);
        var control = new TransferControl(queue, journal, notifier, opener, strategies);

        var result = await control.DiscardAsync(job.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        File.Exists(first).Should().BeFalse();
        File.Exists(second).Should().BeFalse();
        queue.Find(job.Id).Should().BeNull();
        await opener.DidNotReceiveWithAnyArgs().OpenAsync(default, default);
        await journal.Received(1).CheckpointAsync(job, CancellationToken.None);
    }

    [TestMethod]
    public void RecoveryMetadata_PreservesRequestsThatDisplayDetailsCannotRepresent()
    {
        var request = Request() with { SourcePath = "/remote/" + new string('x', 1500) + "/password=filename" };
        var state = new TransferRecoveryState(request, "/tmp/target", ["/tmp/.target.part"]);
        var data = JsonSerializer.Serialize(state);
        var operation = new Operation
        {
            Id = Guid.NewGuid(),
            Kind = TransferOperationKinds.Download,
            Status = OperationStatus.Failed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RecoveryData = data,
        };

        var restored = TransferOperationDetails.TryRead(operation);

        restored.Should().Be(request);
    }

    [TestMethod]
    public void MalformedRecoveryMetadata_IsNotReplacedWithUnreliableDisplayDetails()
    {
        var operation = new Operation
        {
            Id = Guid.NewGuid(),
            Kind = TransferOperationKinds.Download,
            Status = OperationStatus.Failed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RecoveryData = "not json",
        };

        TransferOperationDetails.TryRead(operation).Should().BeNull();
    }

    private static TransferRequest Request()
    {
        return new TransferRequest
        {
            ConnectionId = Guid.NewGuid(),
            ServerName = "offline",
            Direction = TransferDirection.Download,
            SourcePath = "/remote/file",
            TargetDirectory = "/tmp",
        };
    }
}
