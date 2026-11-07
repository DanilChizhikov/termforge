using System.Text.Json;
using DTech.Core.AiBridge.Settings;
using DTech.Persistence.Settings;
using DTech.Persistence.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DTech.Persistence.Tests.Settings;

[TestClass]
public sealed class AiBridgeTokenRepositoryTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AddAndDelete_AreIndependentAndPersistAcrossContexts()
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        var first = NewToken("First");
        var second = NewToken("Second");
        await AddAsync(database, first);
        await AddAsync(database, second);

        using (var context = database.CreateContext())
        {
            var repository = new AiBridgeTokenRepository(context);
            (await repository.DeleteAsync(first.Id, TestContext.CancellationToken)).Should().BeTrue();
            (await repository.DeleteAsync(first.Id, TestContext.CancellationToken)).Should().BeFalse();
            var settings = new AiBridgeSettings(true, false);
            var store = new AiBridgeSettingsStore(context, TimeProvider.System);
            await store.SaveAsync(settings, TestContext.CancellationToken);
        }

        using var reloadContext = database.CreateContext();
        var reload = new AiBridgeTokenRepository(reloadContext);
        var listed = await reload.ListAsync(TestContext.CancellationToken);
        listed.Should().ContainSingle().Which.Should().Be(new AiBridgeTokenInfo(second.Id, second.Name, second.CreatedAt));
        listed[0].CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        var hashes = await reload.ListHashesAsync(TestContext.CancellationToken);
        hashes.Should().Equal(second.TokenHash);
        (await reload.HasAnyAsync(TestContext.CancellationToken)).Should().BeTrue();
    }

    [TestMethod]
    public async Task ConcurrentAdds_KeepBothTokens()
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        var first = NewToken("First");
        var second = NewToken("Second");
        var additions = new[] { Task.Run(() => AddAsync(database, first)), Task.Run(() => AddAsync(database, second)) };

        var results = await Task.WhenAll(additions);

        results.Should().OnlyContain(added => added);
        using var context = database.CreateContext();
        var repository = new AiBridgeTokenRepository(context);
        var tokens = await repository.ListAsync(TestContext.CancellationToken);
        tokens.Should().HaveCount(2);
    }

    [TestMethod]
    [DataRow("Agent", "agent")]
    [DataRow("Агент", "агент")]
    public async Task ConcurrentDuplicateNames_OnlyOneSucceeds(string firstName, string secondName)
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        var first = NewToken(firstName);
        var second = NewToken(secondName);
        var additions = new[] { Task.Run(() => AddAsync(database, first)), Task.Run(() => AddAsync(database, second)) };

        var results = await Task.WhenAll(additions);

        results.Count(added => added).Should().Be(1);
    }

    [TestMethod]
    public async Task SettingsUpdate_ConcurrentWithRevocation_DoesNotRestoreToken()
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        var token = NewToken("Agent");
        await AddAsync(database, token);
        using var tokenContext = database.CreateContext();
        using var settingsContext = database.CreateContext();
        var repository = new AiBridgeTokenRepository(tokenContext);
        var store = new AiBridgeSettingsStore(settingsContext, TimeProvider.System);
        var settings = new AiBridgeSettings(true, false);
        var writes = new Task[]
        {
            Task.Run(() => repository.DeleteAsync(token.Id, TestContext.CancellationToken)),
            Task.Run(() => store.SaveAsync(settings, TestContext.CancellationToken)),
        };

        await Task.WhenAll(writes);

        (await repository.HasAnyAsync(TestContext.CancellationToken)).Should().BeFalse();
        (await store.LoadAsync(TestContext.CancellationToken)).Should().Be(settings);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public async Task Migration_PreservesLegacyTokenAndPreferences(bool enabled, bool requireApproval)
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        const string secret = "existing-client-token";
        var hash = AiBridgeTokenHasher.Hash(secret);
        var document = new { Enabled = enabled, RequireApproval = requireApproval, TokenHash = hash };
        var json = JsonSerializer.Serialize(document);
        await SeedLegacyAsync(database, json);

        await database.MigrateAsync();

        using var context = database.CreateContext();
        var repository = new AiBridgeTokenRepository(context);
        var tokens = await repository.ListAsync(TestContext.CancellationToken);
        tokens.Should().ContainSingle().Which.Name.Should().Be("Default");
        var hashes = await repository.ListHashesAsync(TestContext.CancellationToken);
        hashes.Should().Equal(hash);
        var store = new AiBridgeSettingsStore(context, TimeProvider.System);
        (await store.LoadAsync(TestContext.CancellationToken)).Should().Be(new AiBridgeSettings(enabled, requireApproval));
        var saved = await context.AppSettings.SingleAsync(TestContext.CancellationToken);
        saved.Value.Should().NotContain("TokenHash").And.NotContain(hash).And.NotContain(secret);
        var generator = NSubstitute.Substitute.For<IAiBridgeTokenGenerator>();
        var service = new AiBridgeTokenService(repository, generator, TimeProvider.System);
        (await service.AcceptsAsync(secret, TestContext.CancellationToken)).Should().BeTrue();
        await repository.DeleteAsync(tokens[0].Id, TestContext.CancellationToken);
        await database.MigrateAsync();
        (await service.AcceptsAsync(secret, TestContext.CancellationToken)).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("{\"Enabled\":true,\"RequireApproval\":true,\"TokenHash\":null}")]
    [DataRow("{\"Enabled\":true,\"RequireApproval\":true,\"TokenHash\":\"\"}")]
    [DataRow("{\"Enabled\":true,\"RequireApproval\":true}")]
    [DataRow("invalid-json")]
    public async Task Migration_WithoutLegacyHash_DoesNotCreateToken(string json)
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        await SeedLegacyAsync(database, json);

        await database.MigrateAsync();

        using var context = database.CreateContext();
        var repository = new AiBridgeTokenRepository(context);
        (await repository.HasAnyAsync(TestContext.CancellationToken)).Should().BeFalse();
    }

    private async Task SeedLegacyAsync(TemporaryDatabase database, string json)
    {
        using var context = database.CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261101140000_AddOperationRecovery", TestContext.CancellationToken);
        var setting = new AppSetting { Key = AiBridgeSettingsStore.SettingKey, Value = json, UpdatedAt = DateTime.UtcNow };
        context.AppSettings.Add(setting);
        await context.SaveChangesAsync(TestContext.CancellationToken);
    }

    private async Task<bool> AddAsync(TemporaryDatabase database, AiBridgeTokenRecord token)
    {
        using var context = database.CreateContext();
        var repository = new AiBridgeTokenRepository(context);
        return await repository.TryAddAsync(token, TestContext.CancellationToken);
    }

    private static AiBridgeTokenRecord NewToken(string name)
    {
        var hash = AiBridgeTokenHasher.Hash(name);
        return new AiBridgeTokenRecord
        {
            Id = Guid.NewGuid(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            TokenHash = hash,
            CreatedAt = DateTime.UtcNow,
        };
    }

}
