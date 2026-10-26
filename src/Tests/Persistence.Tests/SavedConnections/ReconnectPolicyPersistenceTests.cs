using DTech.Core.Reconnect;
using DTech.Core.SavedConnections;
using DTech.Persistence.Database;
using DTech.Persistence.SavedConnections;
using DTech.Persistence.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Persistence.Tests.SavedConnections;

[TestClass]
public sealed class ReconnectPolicyPersistenceTests
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
    public async Task NewConnection_WithoutExplicitPolicy_UsesTheDefaultPolicy()
    {
        var connection = Create("web", ReconnectPolicy.Default);

        await AddAsync(connection);
        var found = await FindAsync(connection.Id);

        found!.Details.ReconnectPolicy.Should().Be(ReconnectPolicy.Default);
        ReconnectPolicy.Default.Mode.Should().Be(ReconnectMode.Manual);
    }

    [TestMethod]
    public async Task AddAndUpdate_RoundTripTheReconnectPolicy()
    {
        var connection = Create("web", new ReconnectPolicy(ReconnectMode.Automatic, 8, 3, 90));
        await AddAsync(connection);

        var changed = connection with { Details = connection.Details with { ReconnectPolicy = new ReconnectPolicy(ReconnectMode.Off, 1, 1, 1) } };
        using (var scope = _database.Services.CreateScope())
        {
            var repository = new SavedConnectionRepository(scope.ServiceProvider.GetRequiredService<TermForgeDbContext>());
            await repository.UpdateAsync(changed, TestContext.CancellationToken);
        }

        var stored = _database.Query("SELECT reconnect_mode, reconnect_max_attempts FROM saved_connections");
        var found = await FindAsync(connection.Id);

        stored.Single()["reconnect_mode"].Should().Be("Off");
        found!.Details.ReconnectPolicy.Should().Be(new ReconnectPolicy(ReconnectMode.Off, 1, 1, 1));
    }

    private static SavedConnection Create(string name, ReconnectPolicy policy)
    {
        var details = new SavedConnectionDraft { Name = name, Host = "example.org", UserName = "deploy", ReconnectPolicy = policy };
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return new SavedConnection { Id = Guid.NewGuid(), Details = details, CreatedAt = now, UpdatedAt = now };
    }

    private async Task AddAsync(SavedConnection connection)
    {
        using var scope = _database.Services.CreateScope();
        var repository = new SavedConnectionRepository(scope.ServiceProvider.GetRequiredService<TermForgeDbContext>());
        await repository.AddAsync(connection, TestContext.CancellationToken);
    }

    private async Task<SavedConnection?> FindAsync(Guid id)
    {
        using var scope = _database.Services.CreateScope();
        var repository = new SavedConnectionRepository(scope.ServiceProvider.GetRequiredService<TermForgeDbContext>());
        return await repository.FindAsync(id, TestContext.CancellationToken);
    }
}
