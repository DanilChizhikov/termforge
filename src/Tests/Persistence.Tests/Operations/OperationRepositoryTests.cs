using DTech.Core.Operations;
using DTech.Persistence.Database;
using DTech.Persistence.Operations;
using DTech.Persistence.Tests.Infrastructure;
using FluentAssertions;

namespace DTech.Persistence.Tests.Operations;

[TestClass]
public sealed class OperationRepositoryTests
{
    private static readonly DateTime BaseTime = new(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc);

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
    public async Task Round_trip_preserves_all_fields_and_utc_kind()
    {
        var details = new Dictionary<string, string> { ["path"] = "/srv/app", ["bytes"] = "42" };
        var operation = NewOperation("transfer", "web-1", OperationStatus.Failed, BaseTime) with
        {
            StartedAt = BaseTime.AddSeconds(1),
            FinishedAt = BaseTime.AddSeconds(5),
            Error = "connection lost",
            Retryable = true,
            Details = details,
        };
        await using var writeContext = CreateRepository(out var writer);
        await writer.Add(operation, TestContext.CancellationToken);

        await using var readContext = CreateRepository(out var reader);
        var loaded = await reader.Find(operation.Id, TestContext.CancellationToken);

        loaded.Should().NotBeNull();
        loaded!.Kind.Should().Be("transfer");
        loaded.Server.Should().Be("web-1");
        loaded.Status.Should().Be(OperationStatus.Failed);
        loaded.CreatedAt.Should().Be(BaseTime);
        loaded.CreatedAt.Kind.Should().Be(DateTimeKind.Utc);
        loaded.StartedAt.Should().Be(BaseTime.AddSeconds(1));
        loaded.FinishedAt.Should().Be(BaseTime.AddSeconds(5));
        loaded.Error.Should().Be("connection lost");
        loaded.Retryable.Should().BeTrue();
        loaded.Details.Should().BeEquivalentTo(details);
    }

    [TestMethod]
    public async Task Update_after_add_in_same_context_persists_changes()
    {
        var operation = NewOperation("transfer", null, OperationStatus.Queued, BaseTime);
        await using var context = CreateRepository(out var repository);
        await repository.Add(operation, TestContext.CancellationToken);

        var running = operation with { Status = OperationStatus.Running, StartedAt = BaseTime };
        await repository.Update(running, TestContext.CancellationToken);

        var loaded = await repository.Find(operation.Id, TestContext.CancellationToken);
        loaded!.Status.Should().Be(OperationStatus.Running);
    }

    [TestMethod]
    public async Task Find_unknown_id_returns_null()
    {
        await using var context = CreateRepository(out var repository);

        var loaded = await repository.Find(Guid.NewGuid(), TestContext.CancellationToken);

        loaded.Should().BeNull();
    }

    [TestMethod]
    public async Task Query_filters_by_kind_status_and_server()
    {
        await Seed(
            NewOperation("transfer", "web-1", OperationStatus.Succeeded, BaseTime),
            NewOperation("transfer", "web-2", OperationStatus.Failed, BaseTime.AddMinutes(1)),
            NewOperation("docker", "web-1", OperationStatus.Failed, BaseTime.AddMinutes(2)));
        await using var context = CreateRepository(out var repository);

        var byKind = await repository.Query(Query(kind: "transfer"), TestContext.CancellationToken);
        var byStatus = await repository.Query(Query(status: OperationStatus.Failed), TestContext.CancellationToken);
        var byServer = await repository.Query(Query(server: "web-1"), TestContext.CancellationToken);
        var combined = await repository.Query(Query(kind: "docker", status: OperationStatus.Failed, server: "web-1"), TestContext.CancellationToken);

        byKind.Total.Should().Be(2);
        byStatus.Total.Should().Be(2);
        byServer.Total.Should().Be(2);
        combined.Items.Should().ContainSingle();
    }

    [TestMethod]
    public async Task Query_filters_by_time_range_inclusive()
    {
        await Seed(
            NewOperation("a", null, OperationStatus.Queued, BaseTime),
            NewOperation("b", null, OperationStatus.Queued, BaseTime.AddHours(1)),
            NewOperation("c", null, OperationStatus.Queued, BaseTime.AddHours(2)));
        await using var context = CreateRepository(out var repository);

        var page = await repository.Query(Query(from: BaseTime.AddHours(1), to: BaseTime.AddHours(2)), TestContext.CancellationToken);

        page.Items.Select(item => item.Kind).Should().BeEquivalentTo("b", "c");
    }

    [TestMethod]
    public async Task Query_orders_newest_first_and_pages()
    {
        var operations = new List<Operation>();
        for (var i = 0; i < 5; i++)
        {
            operations.Add(NewOperation($"kind{i}", null, OperationStatus.Queued, BaseTime.AddMinutes(i)));
        }

        await Seed(operations.ToArray());
        await using var context = CreateRepository(out var repository);

        var firstPage = await repository.Query(Query(offset: 0, limit: 2), TestContext.CancellationToken);
        var secondPage = await repository.Query(Query(offset: 2, limit: 2), TestContext.CancellationToken);

        firstPage.Total.Should().Be(5);
        firstPage.Items.Select(item => item.Kind).Should().Equal("kind4", "kind3");
        secondPage.Items.Select(item => item.Kind).Should().Equal("kind2", "kind1");
    }

    private static OperationQuery Query(
        string? kind = null,
        OperationStatus? status = null,
        string? server = null,
        DateTime? from = null,
        DateTime? to = null,
        int offset = 0,
        int limit = 50)
    {
        return new OperationQuery(kind, status, server, from, to, offset, limit);
    }

    private static Operation NewOperation(string kind, string? server, OperationStatus status, DateTime createdAt)
    {
        return new Operation
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            Server = server,
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
    }

    private async Task Seed(params Operation[] operations)
    {
        await using var context = CreateRepository(out var repository);
        for (var i = 0; i < operations.Length; i++)
        {
            await repository.Add(operations[i], TestContext.CancellationToken);
        }
    }

    private TermForgeDbContext CreateRepository(out OperationRepository repository)
    {
        var context = _database.CreateContext();
        repository = new OperationRepository(context);
        return context;
    }
}
