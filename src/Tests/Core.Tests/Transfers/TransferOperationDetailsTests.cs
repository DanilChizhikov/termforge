using DTech.Core.Operations;
using DTech.Core.Transfers;
using FluentAssertions;

namespace DTech.Core.Tests.Transfers;

[TestClass]
public sealed class TransferOperationDetailsTests
{
    [TestMethod]
    public void TryRead_DescribedOperation_RestoresRequest()
    {
        var request = new TransferRequest
        {
            ConnectionId = Guid.NewGuid(),
            ServerName = "web-01",
            Direction = TransferDirection.Download,
            SourcePath = "/var/log/app.log",
            TargetDirectory = "/home/me/Downloads",
            Overwrite = true,
        };
        var operation = CreateOperation(TransferOperationDetails.Describe(request, "/home/me/Downloads/app.log"));

        var restored = TransferOperationDetails.TryRead(operation);

        restored.Should().Be(request);
    }

    [TestMethod]
    public void TryRead_MissingDetails_ReturnsNull()
    {
        var details = new Dictionary<string, string> { [TransferOperationDetails.SourceKey] = "/a" };
        var operation = CreateOperation(details);

        var restored = TransferOperationDetails.TryRead(operation);

        restored.Should().BeNull();
    }

    private static Operation CreateOperation(IReadOnlyDictionary<string, string> details)
    {
        return new Operation
        {
            Id = Guid.NewGuid(),
            Kind = TransferOperationKinds.Download,
            Server = "web-01",
            Status = OperationStatus.Failed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Details = details,
            Retryable = true,
        };
    }
}
