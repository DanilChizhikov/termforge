using DTech.Core.SavedConnections;
using DTech.Persistence.Database;
using DTech.Persistence.SavedConnections;
using DTech.Persistence.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Persistence.Tests.SavedConnections;

[TestClass]
public sealed class ReadOnlyPersistenceTests
{
    private TemporaryDatabase _database = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public async Task Initialize()
    {
        _database = new TemporaryDatabase();
        await _database.MigrateAsync();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _database.Dispose();
    }

    [TestMethod]
    public async Task NewConnection_IsWritableByDefault()
    {
        var connection = Create("web", false);

        await AddAsync(connection);
        var found = await FindAsync(connection.Id);

        found!.Details.ReadOnly.Should().BeFalse();
    }

    [TestMethod]
    public async Task AddAndUpdate_RoundTripTheReadOnlyFlag()
    {
        var connection = Create("db-01", true);
        await AddAsync(connection);
        var stored = _database.Query("SELECT read_only FROM saved_connections");
        stored.Single()["read_only"].Should().Be(1L);

        var changed = connection with { Details = connection.Details with { ReadOnly = false } };
        using (var scope = _database.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<TermForgeDbContext>();
            var repository = new SavedConnectionRepository(context);
            await repository.UpdateAsync(changed, TestContext.CancellationToken);
        }

        var found = await FindAsync(connection.Id);
        found!.Details.ReadOnly.Should().BeFalse();
    }

    private static SavedConnection Create(string name, bool readOnly)
    {
        var details = new SavedConnectionDraft { Name = name, Host = "example.org", UserName = "deploy", ReadOnly = readOnly };
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return new SavedConnection { Id = Guid.NewGuid(), Details = details, CreatedAt = now, UpdatedAt = now };
    }

    private async Task AddAsync(SavedConnection connection)
    {
        using var scope = _database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TermForgeDbContext>();
        var repository = new SavedConnectionRepository(context);
        await repository.AddAsync(connection, TestContext.CancellationToken);
    }

    private async Task<SavedConnection?> FindAsync(Guid id)
    {
        using var scope = _database.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TermForgeDbContext>();
        var repository = new SavedConnectionRepository(context);
        return await repository.FindAsync(id, TestContext.CancellationToken);
    }
}
