using DTech.Core.Operations;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Operations;

[TestClass]
public sealed class OperationJournalTests
{
    private static readonly DateTimeOffset StartTime = new(2026, 10, 25, 10, 0, 0, TimeSpan.Zero);

    private InMemoryOperationRepository _repository = null!;
    private IOperationNotifier _notifier = null!;
    private IOperationRetryHandler _retryHandler = null!;
    private OperationJournal _journal = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _repository = new InMemoryOperationRepository();
        _notifier = Substitute.For<IOperationNotifier>();
        _retryHandler = Substitute.For<IOperationRetryHandler>();
        _retryHandler.Kind.Returns("transfer");
        var timeProvider = new FixedTimeProvider(StartTime);
        var handlers = new[] { _retryHandler };
        _journal = new OperationJournal(_repository, new OperationSanitizer(), _notifier, handlers, timeProvider);
    }

    [TestMethod]
    public async Task Create_stores_queued_operation_and_notifies()
    {
        var request = new CreateOperationRequest("transfer", "web-1", null, true);

        var operation = await _journal.Create(request, TestContext.CancellationToken);

        operation.Status.Should().Be(OperationStatus.Queued);
        operation.CreatedAt.Should().Be(StartTime.UtcDateTime);
        operation.Retryable.Should().BeTrue();
        await _notifier.Received(1).OperationChanged(operation, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task Create_without_kind_is_rejected()
    {
        var request = new CreateOperationRequest(" ", null, null, false);

        var create = () => _journal.Create(request, TestContext.CancellationToken);

        var failure = await create.Should().ThrowAsync<OperationException>();
        failure.Which.Failure.Should().Be(OperationFailure.InvalidRequest);
    }

    [TestMethod]
    public async Task Create_strips_secrets_from_details()
    {
        var details = new Dictionary<string, string> { ["password"] = "hunter2", ["path"] = "/srv" };
        var request = new CreateOperationRequest("transfer", null, details, false);

        var operation = await _journal.Create(request, TestContext.CancellationToken);

        operation.Details["password"].Should().Be(OperationSanitizer.Redacted);
        operation.Details["path"].Should().Be("/srv");
    }

    [TestMethod]
    public async Task Full_success_lifecycle_sets_timestamps()
    {
        var created = await CreateOperation(false);

        var started = await _journal.Start(created.Id, TestContext.CancellationToken);
        var succeeded = await _journal.Succeed(created.Id, new Dictionary<string, string> { ["bytes"] = "10" }, TestContext.CancellationToken);

        started.Status.Should().Be(OperationStatus.Running);
        started.StartedAt.Should().NotBeNull();
        succeeded.Status.Should().Be(OperationStatus.Succeeded);
        succeeded.FinishedAt.Should().NotBeNull();
        succeeded.Details["bytes"].Should().Be("10");
    }

    [TestMethod]
    public async Task Fail_records_sanitized_error()
    {
        var created = await CreateOperation(true);
        await _journal.Start(created.Id, TestContext.CancellationToken);

        var failed = await _journal.Fail(created.Id, "auth failed token=abc123", TestContext.CancellationToken);

        failed.Status.Should().Be(OperationStatus.Failed);
        failed.Error.Should().NotContain("abc123");
    }

    [TestMethod]
    public async Task Queued_operation_can_be_cancelled()
    {
        var created = await CreateOperation(false);

        var cancelled = await _journal.Cancel(created.Id, TestContext.CancellationToken);

        cancelled.Status.Should().Be(OperationStatus.Cancelled);
    }

    [TestMethod]
    public async Task Terminal_operation_rejects_further_transitions()
    {
        var created = await CreateOperation(false);
        await _journal.Start(created.Id, TestContext.CancellationToken);
        await _journal.Succeed(created.Id, null, TestContext.CancellationToken);

        var fail = () => _journal.Fail(created.Id, "late", TestContext.CancellationToken);
        var restart = () => _journal.Start(created.Id, TestContext.CancellationToken);

        (await fail.Should().ThrowAsync<OperationException>()).Which.Failure.Should().Be(OperationFailure.InvalidTransition);
        (await restart.Should().ThrowAsync<OperationException>()).Which.Failure.Should().Be(OperationFailure.InvalidTransition);
    }

    [TestMethod]
    public async Task Queued_operation_cannot_succeed_directly()
    {
        var created = await CreateOperation(false);

        var succeed = () => _journal.Succeed(created.Id, null, TestContext.CancellationToken);

        (await succeed.Should().ThrowAsync<OperationException>()).Which.Failure.Should().Be(OperationFailure.InvalidTransition);
    }

    [TestMethod]
    public async Task Unknown_operation_is_not_found()
    {
        var start = () => _journal.Start(Guid.NewGuid(), TestContext.CancellationToken);

        (await start.Should().ThrowAsync<OperationException>()).Which.Failure.Should().Be(OperationFailure.NotFound);
    }

    [TestMethod]
    public async Task Retry_of_failed_retryable_operation_invokes_handler()
    {
        var created = await CreateOperation(true);
        await _journal.Start(created.Id, TestContext.CancellationToken);
        var failed = await _journal.Fail(created.Id, "boom", TestContext.CancellationToken);

        await _journal.Retry(created.Id, TestContext.CancellationToken);

        await _retryHandler.Received(1).Retry(failed, TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task Retry_of_non_retryable_operation_is_rejected()
    {
        var created = await CreateOperation(false);
        await _journal.Start(created.Id, TestContext.CancellationToken);
        await _journal.Fail(created.Id, "boom", TestContext.CancellationToken);

        var retry = () => _journal.Retry(created.Id, TestContext.CancellationToken);

        (await retry.Should().ThrowAsync<OperationException>()).Which.Failure.Should().Be(OperationFailure.NotRetryable);
    }

    [TestMethod]
    public async Task Retry_of_running_operation_is_rejected()
    {
        var created = await CreateOperation(true);
        await _journal.Start(created.Id, TestContext.CancellationToken);

        var retry = () => _journal.Retry(created.Id, TestContext.CancellationToken);

        (await retry.Should().ThrowAsync<OperationException>()).Which.Failure.Should().Be(OperationFailure.NotRetryable);
    }

    [TestMethod]
    public async Task Retry_without_handler_for_kind_reports_clear_error()
    {
        var request = new CreateOperationRequest("docker", null, null, true);
        var created = await _journal.Create(request, TestContext.CancellationToken);
        await _journal.Start(created.Id, TestContext.CancellationToken);
        await _journal.Fail(created.Id, "boom", TestContext.CancellationToken);

        var retry = () => _journal.Retry(created.Id, TestContext.CancellationToken);

        var failure = await retry.Should().ThrowAsync<OperationException>();
        failure.Which.Failure.Should().Be(OperationFailure.NoRetryHandler);
        failure.Which.Message.Should().Contain("docker");
    }

    private Task<Operation> CreateOperation(bool retryable)
    {
        var request = new CreateOperationRequest("transfer", "web-1", null, retryable);
        return _journal.Create(request, TestContext.CancellationToken);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _now;
        }
    }
}
