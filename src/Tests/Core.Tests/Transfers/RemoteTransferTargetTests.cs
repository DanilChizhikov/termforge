using DTech.Core.Transfers;
using DTech.Core.Transfers.Queue;
using FluentAssertions;

namespace DTech.Core.Tests.Transfers;

[TestClass]
public sealed class RemoteTransferTargetTests
{
    [TestMethod]
    public void Matches_UploadAndRemoteCopy_ReserveTheSameDestination()
    {
        var destination = Guid.NewGuid();
        var upload = Request(destination, TransferDirection.Upload);
        var remote = Request(Guid.NewGuid(), TransferDirection.RemoteToRemote) with { TargetConnectionId = destination };
        var first = new TransferTarget(upload, "/srv/a");
        var second = new TransferTarget(remote, "/srv/a");

        first.Matches(second).Should().BeTrue();
        second.Matches(first).Should().BeTrue();
        second.Matches(new TransferTarget(remote with { TargetConnectionId = Guid.NewGuid() }, "/srv/a")).Should().BeFalse();
    }

    [TestMethod]
    public void Matches_LocalAndRemotePaths_AreIndependent()
    {
        var connection = Guid.NewGuid();
        var local = new TransferTarget(Request(connection, TransferDirection.Download), "/srv/a");
        var remote = new TransferTarget(Request(connection, TransferDirection.Upload), "/srv/a");

        local.Matches(remote).Should().BeFalse();
    }

    private static TransferRequest Request(Guid connection, TransferDirection direction)
    {
        return new TransferRequest
        {
            ConnectionId = connection, ServerName = "source", Direction = direction,
            SourcePath = "/source/a", TargetDirectory = "/srv",
        };
    }
}
