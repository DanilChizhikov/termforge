using DTech.Core.Transfers;
using DTech.Core.Transfers.Execution;
using FluentAssertions;

namespace DTech.Core.Tests.Transfers;

[TestClass]
public sealed class TransferPartialNameTests
{
    private static readonly TransferPartialIdentity Identity = new()
    {
        Direction = TransferDirection.Upload,
        ConnectionId = Guid.Parse("8d7a2f53-1d55-4f4a-9b3c-3c7f2d1e0a11"),
        SourcePath = "/home/me/report.pdf",
        TargetPath = "/srv/report.pdf",
        SourceSize = 1024,
        SourceModifiedUtc = new DateTime(2026, 10, 26, 8, 0, 0, DateTimeKind.Utc),
    };

    [TestMethod]
    public void Create_SameIdentity_ReturnsSameHiddenPartialName()
    {
        var first = TransferPartialName.Create(Identity, "report.pdf");
        var second = TransferPartialName.Create(Identity, "report.pdf");

        first.Should().Be(second);
        first.Should().StartWith(".report.pdf.").And.EndWith(TransferPartialName.Suffix);
    }

    [TestMethod]
    public void Create_ChangedSourceSize_ReturnsDifferentName()
    {
        var changed = Identity with { SourceSize = 2048 };

        var original = TransferPartialName.Create(Identity, "report.pdf");
        var updated = TransferPartialName.Create(changed, "report.pdf");

        updated.Should().NotBe(original);
    }

    [TestMethod]
    public void Create_ChangedSourceModification_ReturnsDifferentName()
    {
        var changed = Identity with { SourceModifiedUtc = Identity.SourceModifiedUtc.AddSeconds(1) };

        var original = TransferPartialName.Create(Identity, "report.pdf");
        var updated = TransferPartialName.Create(changed, "report.pdf");

        updated.Should().NotBe(original);
    }

    [TestMethod]
    public void Create_VeryLongName_StaysWithinFileNameLimit()
    {
        var longName = new string('a', 255);

        var partial = TransferPartialName.Create(Identity, longName);

        partial.Length.Should().BeLessThanOrEqualTo(255);
    }
}
