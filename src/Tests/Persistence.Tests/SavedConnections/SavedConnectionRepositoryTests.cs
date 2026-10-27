using DTech.Core.SavedConnections;
using DTech.Persistence.SavedConnections;
using DTech.Persistence.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DTech.Persistence.Tests.SavedConnections;

[TestClass]
public sealed class SavedConnectionRepositoryTests
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
    public async Task AddAndFind_RoundTripsEveryFieldWithUtcTimestamps()
    {
        var jumpHost = CreateConnection("bastion");
        await AddAsync(jumpHost);
        var connection = CreateConnection("web") with
        {
            Details = CreateConnection("web").Details with
            {
                GroupName = "prod",
                Port = 2222,
                AuthMethod = ConnectionAuthMethod.KeyFile,
                PrivateKeyPath = "/keys/web",
                JumpHostId = jumpHost.Id,
                SshAlias = "web-alias",
            },
            HasPassword = false,
            LastConnectedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        };

        await AddAsync(connection);
        var found = await FindAsync(connection.Id);

        found.Should().BeEquivalentTo(connection);
        found!.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        found.LastConnectedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [TestMethod]
    public async Task List_ReturnsConnectionsOrderedByName()
    {
        await AddAsync(CreateConnection("zeta"));
        await AddAsync(CreateConnection("alpha"));

        var list = await ListAsync();

        list.Select(connection => connection.Details.Name).Should().Equal("alpha", "zeta");
    }

    [TestMethod]
    public async Task Add_WithNameDifferingOnlyInCase_ViolatesUniqueIndex()
    {
        await AddAsync(CreateConnection("Web"));

        var act = () => AddAsync(CreateConnection("web"));

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [TestMethod]
    public async Task Update_ChangesStoredValues()
    {
        var connection = CreateConnection("web");
        await AddAsync(connection);
        var changed = connection with
        {
            Details = connection.Details with { Host = "10.1.1.1", GroupName = "lab" },
            HasPassword = true,
        };

        var updated = await UseRepository(repository => repository.UpdateAsync(changed, TestContext.CancellationToken));

        updated.Should().BeTrue();
        var found = await FindAsync(connection.Id);
        found!.Details.Host.Should().Be("10.1.1.1");
        found.Details.GroupName.Should().Be("lab");
        found.HasPassword.Should().BeTrue();
    }

    [TestMethod]
    public async Task Update_OfUnknownConnection_ReturnsFalse()
    {
        var updated = await UseRepository(repository => repository.UpdateAsync(CreateConnection("ghost"), TestContext.CancellationToken));

        updated.Should().BeFalse();
    }

    [TestMethod]
    public async Task Delete_RemovesConnection()
    {
        var connection = CreateConnection("web");
        await AddAsync(connection);

        var deleted = await UseRepository(repository => repository.DeleteAsync(connection.Id, TestContext.CancellationToken));

        deleted.Should().BeTrue();
        (await FindAsync(connection.Id)).Should().BeNull();
    }

    [TestMethod]
    public async Task Delete_OfJumpHostStillReferenced_IsRestricted()
    {
        var jumpHost = CreateConnection("bastion");
        await AddAsync(jumpHost);
        var inner = CreateConnection("inner");
        await AddAsync(inner with { Details = inner.Details with { JumpHostId = jumpHost.Id } });

        var act = () => UseRepository(repository => repository.DeleteAsync(jumpHost.Id, TestContext.CancellationToken));

        await act.Should().ThrowAsync<Exception>();
    }

    [TestMethod]
    public async Task RecordConnected_StoresTimestamp()
    {
        var connection = CreateConnection("web");
        await AddAsync(connection);
        var connectedAt = new DateTime(2026, 5, 6, 7, 8, 9, DateTimeKind.Utc);

        await UseRepository(repository => repository.RecordConnectedAsync(connection.Id, connectedAt, TestContext.CancellationToken));

        var found = await FindAsync(connection.Id);
        found!.LastConnectedAt.Should().Be(connectedAt);
    }

    [TestMethod]
    public async Task RenameGroup_AndRemoveGroup_UpdateOnlyMatchingConnections()
    {
        var first = CreateConnection("one");
        var second = CreateConnection("two");
        await AddAsync(first with { Details = first.Details with { GroupName = "old" } });
        await AddAsync(second with { Details = second.Details with { GroupName = "keep" } });

        await UseRepository(repository => repository.RenameGroupAsync("old", "new", TestContext.CancellationToken));
        var afterRename = await ListAsync();
        await UseRepository(repository => repository.RemoveGroupAsync("new", TestContext.CancellationToken));
        var afterRemove = await ListAsync();

        afterRename.Select(connection => connection.Details.GroupName).Should().Equal("new", "keep");
        afterRemove.Select(connection => connection.Details.GroupName).Should().Equal(null, "keep");
    }

    [TestMethod]
    public void Table_HasNoColumnThatCouldHoldAPassword()
    {
        var columns = _database.Query("SELECT name FROM pragma_table_info('saved_connections')")
            .Select(row => (string)row["name"]!)
            .ToList();

        columns.Should().Contain("has_password");
        columns.Where(name => name.Contains("password", StringComparison.OrdinalIgnoreCase)).Should().Equal("has_password");
        columns.Should().NotContain(name => name.Contains("secret", StringComparison.OrdinalIgnoreCase));
    }

    private static SavedConnection CreateConnection(string name)
    {
        var details = new SavedConnectionDraft { Name = name, Host = "example.org", UserName = "deploy" };
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return new SavedConnection { Id = Guid.NewGuid(), Details = details, CreatedAt = now, UpdatedAt = now };
    }

    private Task AddAsync(SavedConnection connection)
    {
        return UseRepository(async repository =>
        {
            await repository.AddAsync(connection, TestContext.CancellationToken);
            return true;
        });
    }

    private Task<SavedConnection?> FindAsync(Guid id)
    {
        return UseRepository(repository => repository.FindAsync(id, TestContext.CancellationToken));
    }

    private Task<IReadOnlyList<SavedConnection>> ListAsync()
    {
        return UseRepository(repository => repository.ListAsync(TestContext.CancellationToken));
    }

    private async Task<TResult> UseRepository<TResult>(Func<SavedConnectionRepository, Task<TResult>> action)
    {
        await using var context = _database.CreateContext();
        var repository = new SavedConnectionRepository(context);
        return await action(repository);
    }
}
