using System.Text.Json;
using DTech.Core.Operations;
using DTech.Core.Transfers;
using DTech.Persistence.Operations;
using DTech.Persistence.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DTech.Persistence.Tests.Operations;

[TestClass]
public sealed class OperationMaintenanceTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Migrate_LegacyOperation_IsPreservedWithoutInventingPartialPaths()
    {
        using var database = new TemporaryDatabase();
        var directory = Path.GetDirectoryName(database.DatabasePath)!;
        Directory.CreateDirectory(directory);
        await using (var legacy = database.CreateContext())
        {
            var migrator = legacy.GetService<IMigrator>();
            await migrator.MigrateAsync("20261030070247_AddServerAiAccess", TestContext.CancellationToken);
            var id = Guid.NewGuid();
            var kind = TransferOperationKinds.Download;
            var details = JsonSerializer.Serialize(TransferOperationDetails.Describe(Request(), "/tmp/target"));
            await legacy.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO operations (id, kind, status, created_at, updated_at, details, retryable) VALUES ({id}, {kind}, 'Running', {Now}, {Now}, {details}, 1)", TestContext.CancellationToken);
        }

        await database.MigrateAsync();
        await using var context = database.CreateContext();
        var maintenance = new OperationMaintenance(context);
        var policy = new OperationRecoveryPolicy(Now, [TransferOperationKinds.Download]);
        await maintenance.RecoverAsync(policy, TestContext.CancellationToken);

        var restored = await context.Operations.AsNoTracking().SingleAsync(TestContext.CancellationToken);
        restored.Status.Should().Be(OperationStatus.Failed);
        restored.Retryable.Should().BeTrue();
        restored.RecoveryData.Should().BeNull();
        restored.HasPendingCleanup.Should().BeFalse();
        var pending = await maintenance.ListPendingCleanupAsync(TestContext.CancellationToken);
        pending.Should().BeEmpty();
        context.Database.HasPendingModelChanges().Should().BeFalse();
    }

    [TestMethod]
    public async Task Recover_MarksPreviousWorkFailedAndOnlyRegisteredKindsRetryable()
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        await using var context = database.CreateContext();
        var queued = NewOperation(OperationStatus.Queued, Now.AddMinutes(-2));
        var running = NewOperation(OperationStatus.Running, Now.AddMinutes(-1));
        var unsupported = NewOperation(OperationStatus.Running, Now) with { Kind = "docker.restart" };
        var completed = NewOperation(OperationStatus.Succeeded, Now);
        context.Operations.AddRange(queued, running, unsupported, completed);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        context.ChangeTracker.Clear();
        var maintenance = new OperationMaintenance(context);
        var policy = new OperationRecoveryPolicy(Now, [TransferOperationKinds.Download]);

        await maintenance.RecoverAsync(policy, TestContext.CancellationToken);

        var operations = await context.Operations.AsNoTracking().ToDictionaryAsync(operation => operation.Id, TestContext.CancellationToken);
        operations[queued.Id].Status.Should().Be(OperationStatus.Failed);
        operations[running.Id].Status.Should().Be(OperationStatus.Failed);
        operations[queued.Id].Retryable.Should().BeTrue();
        operations[running.Id].Retryable.Should().BeTrue();
        operations[unsupported.Id].Retryable.Should().BeFalse();
        operations[completed.Id].Status.Should().Be(OperationStatus.Succeeded);
        operations[running.Id].FinishedAt.Should().Be(Now);
        operations[running.Id].Error.Should().Contain("interrupted");
        operations[running.Id].RecoveryData.Should().Be(running.RecoveryData);
    }

    [TestMethod]
    public async Task Checkpoint_PreservesExactPathsWithoutChangingOperationStatus()
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        await using var context = database.CreateContext();
        var operation = NewOperation(OperationStatus.Running, Now);
        context.Operations.Add(operation);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        context.ChangeTracker.Clear();
        var maintenance = new OperationMaintenance(context);
        var path = "/tmp/" + new string('a', 1400) + "/password=filename.part";
        var state = new TransferRecoveryState(Request(), "/tmp/target", [path]);
        var data = JsonSerializer.Serialize(state);
        var checkpoint = new OperationCheckpoint(operation.Id, data, true);

        await maintenance.CheckpointAsync(checkpoint, TestContext.CancellationToken);

        var loaded = await context.Operations.AsNoTracking().SingleAsync(TestContext.CancellationToken);
        loaded.Status.Should().Be(OperationStatus.Running);
        loaded.HasPendingCleanup.Should().BeTrue();
        var recovered = TransferRecoveryState.TryRead(loaded);
        recovered!.PartialPaths.Should().Equal(path);
    }

    [TestMethod]
    public async Task Prune_AppliesAgeAndCountAndKeepsActiveAndPendingCleanup()
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        await using var context = database.CreateContext();
        var old = NewOperation(OperationStatus.Succeeded, Now.AddDays(-40));
        var pending = NewOperation(OperationStatus.Failed, Now.AddDays(-40)) with { HasPendingCleanup = true };
        var active = NewOperation(OperationStatus.Running, Now.AddDays(-40));
        var oldest = NewOperation(OperationStatus.Cancelled, Now.AddDays(-3));
        var middle = NewOperation(OperationStatus.Failed, Now.AddDays(-2));
        var newest = NewOperation(OperationStatus.Succeeded, Now.AddDays(-1));
        context.Operations.AddRange(old, pending, active, oldest, middle, newest);
        await context.SaveChangesAsync(TestContext.CancellationToken);
        context.ChangeTracker.Clear();
        var maintenance = new OperationMaintenance(context);
        var policy = new OperationRetentionPolicy(Now.AddDays(-30), 2);

        await maintenance.PruneAsync(policy, TestContext.CancellationToken);

        var ids = await context.Operations.Select(operation => operation.Id).ToListAsync(TestContext.CancellationToken);
        Guid[] expected = [pending.Id, active.Id, middle.Id, newest.Id];
        ids.Should().BeEquivalentTo(expected);
    }

    [TestMethod]
    public async Task Recover_OverOneBatch_IsIdempotentAndLeavesNoOldActiveOperations()
    {
        using var database = new TemporaryDatabase();
        await database.MigrateAsync();
        await using var context = database.CreateContext();
        for (var i = 0; i < 205; i++)
        {
            var operation = NewOperation(OperationStatus.Running, Now);
            context.Operations.Add(operation);
        }

        await context.SaveChangesAsync(TestContext.CancellationToken);
        context.ChangeTracker.Clear();
        var maintenance = new OperationMaintenance(context);
        var first = new OperationRecoveryPolicy(Now, [TransferOperationKinds.Download]);
        var second = new OperationRecoveryPolicy(Now.AddHours(1), [TransferOperationKinds.Download]);

        await maintenance.RecoverAsync(first, TestContext.CancellationToken);
        await maintenance.RecoverAsync(second, TestContext.CancellationToken);

        var operations = await context.Operations.AsNoTracking().ToListAsync(TestContext.CancellationToken);
        operations.Should().HaveCount(205).And.OnlyContain(operation => operation.Status == OperationStatus.Failed && operation.FinishedAt == Now);
    }

    private static Operation NewOperation(OperationStatus status, DateTime time)
    {
        var request = Request();
        var recovery = new TransferRecoveryState(request, "/tmp/target", []);
        var data = JsonSerializer.Serialize(recovery);
        return new Operation
        {
            Id = Guid.NewGuid(),
            Kind = TransferOperationKinds.Download,
            Status = status,
            CreatedAt = time,
            UpdatedAt = time,
            RecoveryData = data,
        };
    }

    private static TransferRequest Request()
    {
        return new TransferRequest
        {
            ConnectionId = Guid.NewGuid(),
            ServerName = "server",
            Direction = TransferDirection.Download,
            SourcePath = "/remote/file",
            TargetDirectory = "/tmp",
        };
    }
}
