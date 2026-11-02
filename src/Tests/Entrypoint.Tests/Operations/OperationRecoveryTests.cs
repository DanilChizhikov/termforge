using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DTech.Core.Files;
using DTech.Core.Operations;
using DTech.Core.SavedConnections;
using DTech.Core.Transfers;
using DTech.Core.Transfers.Queue;
using DTech.Entrypoint.Tests.Infrastructure;
using DTech.Entrypoint.Tests.Transfers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Operations;

[TestClass]
public sealed class OperationRecoveryTests
{
    private readonly Guid _connectionId = Guid.NewGuid();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"termforge-recovery-{Guid.NewGuid():N}");
    private ITransferConnectionOpener _opener = null!;
    private IOperationNotifier _notifier = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        Directory.CreateDirectory(_directory);
        _opener = Substitute.For<ITransferConnectionOpener>();
        _notifier = Substitute.For<IOperationNotifier>();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gate.SetResult();
        _opener.OpenAsync(_connectionId, Arg.Any<CancellationToken>()).Returns(_ =>
        {
            var client = new GatedRemoteFileClient(gate);
            return FileResult<IRemoteFileClient>.Success(client);
        });
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(_directory, true);
    }

    [TestMethod]
    public async Task Restart_RestoresPartialWithoutRunningWorkAndDiscardsItOffline()
    {
        var request = Request();
        var partial = Path.Combine(_directory, ".known.part");
        var unrelated = Path.Combine(_directory, ".unrelated.part");
        await File.WriteAllTextAsync(partial, "partial", TestContext.CancellationToken);
        await File.WriteAllTextAsync(unrelated, "keep", TestContext.CancellationToken);
        var operation = await SeedAsync(request, [partial]);
        await using var factory = Factory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);

        using var listed = await TestRequest.Get("/api/transfers").WithSession(session).SendAsync(client, TestContext.CancellationToken);
        var transfers = await listed.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);

        transfers.GetArrayLength().Should().Be(1);
        transfers[0].GetProperty("status").GetString().Should().Be("failed");
        transfers[0].GetProperty("hasPartial").GetBoolean().Should().BeTrue();
        transfers[0].GetProperty("partialBytes").GetInt64().Should().Be(0);
        await _opener.DidNotReceiveWithAnyArgs().OpenAsync(default, default);
        var queue = factory.Services.GetRequiredService<TransferQueue>();
        queue.Find(operation.Id)!.ProcessingFinished.Should().BeTrue();

        using var discarded = await Post(client, session, $"/api/transfers/{operation.Id}/discard");

        discarded.StatusCode.Should().Be(HttpStatusCode.NoContent);
        File.Exists(partial).Should().BeFalse();
        File.Exists(unrelated).Should().BeTrue();
        await _opener.DidNotReceiveWithAnyArgs().OpenAsync(default, default);
        using var response = await TestRequest.Get($"/api/operations/{operation.Id}").WithSession(session).SendAsync(client, TestContext.CancellationToken);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        body.GetProperty("hasPartial").GetBoolean().Should().BeFalse();
        body.TryGetProperty("recoveryData", out _).Should().BeFalse();
        body.GetProperty("error").GetString().Should().Contain("interrupted");
    }

    [TestMethod]
    public async Task Restart_RetryUsesExactScopeAndKeepsOlderPartialUntilDiscarded()
    {
        var targetDirectory = Path.Combine(_directory, "password=local-target");
        Directory.CreateDirectory(targetDirectory);
        var request = Request() with { TargetDirectory = targetDirectory };
        var partial = Path.Combine(targetDirectory, ".old-source.part");
        var target = Path.Combine(targetDirectory, "file.txt");
        await File.WriteAllTextAsync(partial, "old partial", TestContext.CancellationToken);
        await File.WriteAllTextAsync(target, "original", TestContext.CancellationToken);
        var operation = await SeedAsync(request, [partial]);
        await using var factory = Factory();
        using var client = factory.CreateClientWithoutCookies();
        var session = await SessionBootstrapper.AuthenticateAsync(factory, client, TestContext.CancellationToken);
        var retryPath = $"/api/operations/{operation.Id}/retry";

        using var refused = await Post(client, session, retryPath);
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var confirmation = await Post(client, session, $"/api/operations/{operation.Id}/retry-confirmation");
        confirmation.StatusCode.Should().Be(HttpStatusCode.OK);
        var grant = await confirmation.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
        grant.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo("token", "expiresAt");
        var token = grant.GetProperty("token").GetString();
        var options = new OperationRetryOptions(true, token);
        using var retried = await TestRequest.Post(retryPath)
            .WithSession(session)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithCsrfToken(session.CsrfToken)
            .WithJson(options)
            .SendAsync(client, TestContext.CancellationToken);
        retried.StatusCode.Should().Be(HttpStatusCode.Accepted);
        await _notifier.Received(1).OperationChanged(
            Arg.Is<Operation>(updated => updated.Id == operation.Id && !updated.HasPendingCleanup),
            Arg.Any<CancellationToken>());
        var queue = factory.Services.GetRequiredService<TransferQueue>();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        TransferJob? completed = null;
        while (DateTime.UtcNow < deadline)
        {
            completed = queue.List().SingleOrDefault(job => job.Id != operation.Id);
            if (completed?.ProcessingFinished == true)
            {
                break;
            }

            await Task.Delay(20, TestContext.CancellationToken);
        }

        completed.Should().NotBeNull();
        completed!.ProcessingFinished.Should().BeTrue();
        completed.Status.Should().Be(OperationStatus.Succeeded);
        completed.CaptureRecovery().PartialPaths.Should().Equal(partial);
        var content = await File.ReadAllBytesAsync(target, TestContext.CancellationToken);
        content.Should().Equal(GatedRemoteFileClient.Content);
        using var replayed = await TestRequest.Post(retryPath)
            .WithSession(session)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithCsrfToken(session.CsrfToken)
            .WithJson(options)
            .SendAsync(client, TestContext.CancellationToken);
        replayed.StatusCode.Should().Be(HttpStatusCode.PreconditionRequired);
        using var discarded = await Post(client, session, $"/api/transfers/{completed.Id}/discard");
        discarded.StatusCode.Should().Be(HttpStatusCode.NoContent);
        File.Exists(partial).Should().BeFalse();
        File.Exists(target).Should().BeTrue();
    }

    [TestMethod]
    [DataRow(OperationStatus.Succeeded, false)]
    [DataRow(OperationStatus.Cancelled, true)]
    public async Task Restart_FinishedTransferWithOldPartial_PreservesItsStatus(OperationStatus status, bool retryable)
    {
        var request = Request();
        var partial = Path.Combine(_directory, ".old.part");
        await File.WriteAllTextAsync(partial, "old partial", TestContext.CancellationToken);
        var operation = await SeedAsync(request, [partial], status);
        await using var factory = Factory();
        using var client = factory.CreateClientWithoutCookies();
        var queue = factory.Services.GetRequiredService<TransferQueue>();

        var restored = queue.Find(operation.Id)!;

        restored.Status.Should().Be(status);
        restored.Snapshot().Retryable.Should().Be(retryable);
        restored.HasPartial.Should().BeTrue();
        File.Exists(partial).Should().BeTrue();
        await _opener.DidNotReceiveWithAnyArgs().OpenAsync(default, default);
    }

    private async Task<Operation> SeedAsync(TransferRequest request, IReadOnlyList<string> partials, OperationStatus status = OperationStatus.Running)
    {
        await using var factory = Factory();
        using var client = factory.CreateClientWithoutCookies();
        await using var scope = factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOperationRepository>();
        var target = Path.Combine(request.TargetDirectory, "file.txt");
        var state = new TransferRecoveryState(request, target, partials);
        var metadata = JsonSerializer.Serialize(state);
        var details = TransferOperationDetails.Describe(request, target);
        var sanitizer = scope.ServiceProvider.GetRequiredService<IOperationSanitizer>();
        var sanitized = sanitizer.SanitizeDetails(details);
        var now = DateTime.UtcNow;
        var operation = new Operation
        {
            Id = Guid.NewGuid(),
            Kind = TransferOperationKinds.Download,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
            FinishedAt = OperationStateMachine.IsTerminal(status) ? now : null,
            Details = sanitized,
            RecoveryData = metadata,
            HasPendingCleanup = partials.Count > 0,
            Retryable = true,
        };
        await repository.Add(operation, TestContext.CancellationToken);
        return operation;
    }

    private TermForgeApplicationFactory Factory()
    {
        var databasePath = Path.Combine(_directory, "termforge.db");
        var settings = new Dictionary<string, string?> { ["Persistence:DatabasePath"] = databasePath };
        return new TermForgeApplicationFactory(settings) { ConfigureServices = ReplaceRemoteAccess };
    }

    private void ReplaceRemoteAccess(IServiceCollection services)
    {
        var connection = new SavedConnection
        {
            Id = _connectionId,
            Details = new SavedConnectionDraft { Name = "server", Host = "203.0.113.10", UserName = "deploy", ReadOnly = true },
        };
        var repository = Substitute.For<ISavedConnectionRepository>();
        repository.FindAsync(_connectionId, Arg.Any<CancellationToken>()).Returns(connection);
        services.RemoveAll<ISavedConnectionRepository>();
        services.AddSingleton(repository);
        services.RemoveAll<ITransferConnectionOpener>();
        services.AddSingleton(_opener);
        services.RemoveAll<IOperationNotifier>();
        services.AddSingleton(_notifier);
    }

    private Task<HttpResponseMessage> Post(HttpClient client, AuthenticatedSession session, string path)
    {
        return TestRequest.Post(path)
            .WithSession(session)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithCsrfToken(session.CsrfToken)
            .SendAsync(client, TestContext.CancellationToken);
    }

    private TransferRequest Request()
    {
        return new TransferRequest
        {
            ConnectionId = _connectionId,
            ServerName = "server",
            Direction = TransferDirection.Download,
            SourcePath = "/remote/file.txt",
            TargetDirectory = _directory,
        };
    }
}
