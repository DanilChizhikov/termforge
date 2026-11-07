using DTech.Core.AiBridge.Settings;
using DTech.Core.Tests.AiBridge.Support;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.AiBridge;

[TestClass]
public sealed class AiBridgeTokenServiceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Create_TrimsNameAndStoresOnlyHash()
    {
        var repository = Substitute.For<IAiBridgeTokenRepository>();
        repository.TryAddAsync(Arg.Any<AiBridgeTokenRecord>(), Arg.Any<CancellationToken>()).Returns(true);
        var generator = new SequentialTokenGenerator();
        var service = new AiBridgeTokenService(repository, generator, TimeProvider.System);
        var before = DateTime.UtcNow;

        var created = await service.CreateAsync("  Агент  ", TestContext.CancellationToken);

        created.Error.Should().BeNull();
        created.Token.Should().Be("token-1");
        created.Info!.Name.Should().Be("Агент");
        created.Info.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        created.Info.CreatedAt.Should().BeOnOrAfter(before);
        var hash = AiBridgeTokenHasher.Hash(created.Token!);
        await repository.Received(1).TryAddAsync(Arg.Is<AiBridgeTokenRecord>(record =>
            record.Id == created.Info.Id && record.Name == "Агент" && record.NormalizedName == "АГЕНТ"
            && record.TokenHash == hash && record.CreatedAt == created.Info.CreatedAt), TestContext.CancellationToken);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task Create_EmptyName_IsRejected(string? name)
    {
        await AssertInvalidNameAsync(name);
    }

    [TestMethod]
    public async Task Create_LongName_IsRejected()
    {
        var name = new string('a', 101);
        await AssertInvalidNameAsync(name);
    }

    [TestMethod]
    public async Task Create_DuplicateName_DoesNotReturnSecret()
    {
        var repository = Substitute.For<IAiBridgeTokenRepository>();
        var generator = new SequentialTokenGenerator();
        var service = new AiBridgeTokenService(repository, generator, TimeProvider.System);

        var created = await service.CreateAsync("Agent", TestContext.CancellationToken);

        created.Error.Should().Be(AiBridgeTokenCreationError.DuplicateName);
        created.Info.Should().BeNull();
        created.Token.Should().BeNull();
    }

    [TestMethod]
    public async Task Accepts_ChecksAllCurrentHashes()
    {
        var repository = Substitute.For<IAiBridgeTokenRepository>();
        var firstHash = AiBridgeTokenHasher.Hash("first");
        var secondHash = AiBridgeTokenHasher.Hash("second");
        repository.ListHashesAsync(Arg.Any<CancellationToken>()).Returns(new[] { firstHash, secondHash });
        var generator = new SequentialTokenGenerator();
        var service = new AiBridgeTokenService(repository, generator, TimeProvider.System);

        (await service.AcceptsAsync("first", TestContext.CancellationToken)).Should().BeTrue();
        (await service.AcceptsAsync("second", TestContext.CancellationToken)).Should().BeTrue();
        (await service.AcceptsAsync("wrong", TestContext.CancellationToken)).Should().BeFalse();
        repository.ListHashesAsync(Arg.Any<CancellationToken>()).Returns(new[] { secondHash });
        (await service.AcceptsAsync("first", TestContext.CancellationToken)).Should().BeFalse();
        (await service.AcceptsAsync("second", TestContext.CancellationToken)).Should().BeTrue();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public async Task Accepts_EmptyToken_IsRejectedWithoutReadingRepository(string? token)
    {
        var repository = Substitute.For<IAiBridgeTokenRepository>();
        var generator = new SequentialTokenGenerator();
        var service = new AiBridgeTokenService(repository, generator, TimeProvider.System);

        (await service.AcceptsAsync(token, TestContext.CancellationToken)).Should().BeFalse();
        await repository.DidNotReceive().ListHashesAsync(Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Create_MaximumLengthName_IsAccepted()
    {
        var repository = Substitute.For<IAiBridgeTokenRepository>();
        repository.TryAddAsync(Arg.Any<AiBridgeTokenRecord>(), Arg.Any<CancellationToken>()).Returns(true);
        var generator = new SequentialTokenGenerator();
        var service = new AiBridgeTokenService(repository, generator, TimeProvider.System);
        var name = new string('a', 100);

        var created = await service.CreateAsync(name, TestContext.CancellationToken);

        created.Error.Should().BeNull();
        created.Info!.Name.Should().Be(name);
    }

    private async Task AssertInvalidNameAsync(string? name)
    {
        var repository = Substitute.For<IAiBridgeTokenRepository>();
        var generator = Substitute.For<IAiBridgeTokenGenerator>();
        var service = new AiBridgeTokenService(repository, generator, TimeProvider.System);

        var created = await service.CreateAsync(name, TestContext.CancellationToken);

        created.Error.Should().Be(AiBridgeTokenCreationError.InvalidName);
        generator.DidNotReceive().Create();
        await repository.DidNotReceive().TryAddAsync(Arg.Any<AiBridgeTokenRecord>(), Arg.Any<CancellationToken>());
    }
}
