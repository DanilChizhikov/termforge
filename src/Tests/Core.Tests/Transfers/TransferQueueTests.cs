using DTech.Core.Operations;
using DTech.Core.Transfers;
using DTech.Core.Transfers.Queue;
using FluentAssertions;

namespace DTech.Core.Tests.Transfers;

[TestClass]
public sealed class TransferQueueTests
{
    private static readonly Guid ConnectionId = Guid.NewGuid();

    [TestMethod]
    public void TryAdd_SameActiveTarget_IsRejectedAsBusy()
    {
        var queue = new TransferQueue();
        var first = CreateJob("/srv/a.txt");
        var second = CreateJob("/srv/a.txt");

        var firstAdmission = queue.TryAdd(first);
        var secondAdmission = queue.TryAdd(second);

        firstAdmission.Should().Be(TransferAdmission.Added);
        secondAdmission.Should().Be(TransferAdmission.TargetBusy);
    }

    [TestMethod]
    public void TryAdd_TargetOfFinishedTransfer_IsAccepted()
    {
        var queue = new TransferQueue();
        var first = CreateJob("/srv/a.txt");
        queue.TryAdd(first);
        first.RequestCancel();
        var second = CreateJob("/srv/a.txt");

        var admission = queue.TryAdd(second);

        admission.Should().Be(TransferAdmission.Added);
    }

    [TestMethod]
    public void TryAdd_AfterClose_IsRejected()
    {
        var queue = new TransferQueue();
        queue.Close();
        var job = CreateJob("/srv/a.txt");

        var admission = queue.TryAdd(job);

        admission.Should().Be(TransferAdmission.Closed);
        queue.List().Should().BeEmpty();
    }

    [TestMethod]
    public void List_ReturnsJobsInAdmissionOrder()
    {
        var queue = new TransferQueue();
        var first = CreateJob("/srv/1.txt");
        var second = CreateJob("/srv/2.txt");
        var third = CreateJob("/srv/3.txt");
        queue.TryAdd(first);
        queue.TryAdd(second);
        queue.TryAdd(third);

        var listed = queue.List();

        listed.Should().Equal(first, second, third);
    }

    [TestMethod]
    public async Task TryAdd_ConcurrentAdmissionsToSameTarget_AdmitsExactlyOne()
    {
        var queue = new TransferQueue();
        var attempts = new Task<TransferAdmission>[32];
        for (var i = 0; i < attempts.Length; i++)
        {
            var job = CreateJob("/srv/race.bin");
            attempts[i] = Task.Run(() => queue.TryAdd(job));
        }

        var admissions = await Task.WhenAll(attempts);

        admissions.Count(admission => admission == TransferAdmission.Added).Should().Be(1);
    }

    [TestMethod]
    public void RequestCancel_QueuedJob_CancelsImmediatelyAndCannotStart()
    {
        var job = CreateJob("/srv/a.txt");

        var outcome = job.RequestCancel();
        var started = job.TryStart();

        outcome.Should().Be(TransferCancelOutcome.CancelledWhileQueued);
        started.Should().BeFalse();
        job.Status.Should().Be(OperationStatus.Cancelled);
    }

    [TestMethod]
    public void RequestCancel_RunningJob_SignalsCancellationToken()
    {
        var job = CreateJob("/srv/a.txt");
        job.TryStart();

        var outcome = job.RequestCancel();

        outcome.Should().Be(TransferCancelOutcome.CancellationRequested);
        job.CancellationToken.IsCancellationRequested.Should().BeTrue();
    }

    [TestMethod]
    public void Snapshot_CombinesCompletedAndCurrentFileBytes()
    {
        var job = CreateJob("/srv/dir");
        job.TryStart();
        job.Plan(300, 2);
        job.BeginFile("/srv/dir/.a.part", 0);
        job.ReportFileBytes(100);
        job.CompleteFile(100);
        job.BeginFile("/srv/dir/.b.part", 50);
        job.ReportFileBytes(120);

        var snapshot = job.Snapshot();

        snapshot.Progress.TransferredBytes.Should().Be(220);
        snapshot.Progress.TotalBytes.Should().Be(300);
        snapshot.Progress.CompletedFiles.Should().Be(1);
        snapshot.Progress.TotalFiles.Should().Be(2);
    }

    private static TransferJob CreateJob(string target)
    {
        var request = new TransferRequest
        {
            ConnectionId = ConnectionId,
            ServerName = "web-01",
            Direction = TransferDirection.Upload,
            SourcePath = "/local/a.txt",
            TargetDirectory = "/srv",
        };
        return new TransferJob(Guid.NewGuid(), request, target);
    }
}
