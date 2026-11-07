using DTech.Core.Transfers;
using DTech.Core.Transfers.Queue;
using FluentAssertions;

namespace DTech.Core.Tests.Transfers;

[TestClass]
public sealed class TransferVerificationTests
{
    [TestMethod]
    public void VerificationProgress_DoesNotCountAsTransferredContent()
    {
        var request = new TransferRequest
        {
            ConnectionId = Guid.NewGuid(), TargetConnectionId = Guid.NewGuid(), ServerName = "source",
            Direction = TransferDirection.RemoteToRemote, SourcePath = "/a", TargetDirectory = "/srv",
        };
        var job = new TransferJob(Guid.NewGuid(), request, "/srv/a");
        job.BeginVerification(200);

        job.ReportVerification(100);

        var checking = job.Snapshot();
        checking.Verification.Should().Be(new TransferVerification(100, 200));
        checking.Progress.TransferredBytes.Should().Be(0);
        job.BeginFile("/srv/.a.part", 50);
        job.Snapshot().Verification.Should().BeNull();
        job.Progress().TransferredBytes.Should().Be(50);
        job.FinishProcessing();
    }
}
