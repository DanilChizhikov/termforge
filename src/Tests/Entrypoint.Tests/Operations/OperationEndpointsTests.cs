using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DTech.Core.Operations;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Operations;

[TestClass]
public sealed class OperationEndpointsTests
{
    private const string ListPath = "/api/operations";

    private IOperationRetryHandler _retryHandler = null!;
    private TermForgeApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _retryHandler = Substitute.For<IOperationRetryHandler>();
        _retryHandler.Kind.Returns("transfer");
        _factory = new TermForgeApplicationFactory
        {
            ConfigureServices = services => services.AddSingleton(_retryHandler),
        };
        _client = _factory.CreateClientWithoutCookies();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [TestMethod]
    public async Task List_without_session_is_unauthorized()
    {
        using var response = await TestRequest.Get(ListPath).SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task Retry_without_session_is_unauthorized()
    {
        var path = $"{ListPath}/{Guid.NewGuid()}/retry";

        using var response = await TestRequest.Post(path)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [TestMethod]
    public async Task List_returns_filtered_and_paged_operations()
    {
        var session = await Authenticate();
        await CreateOperation("transfer", "web-1");
        var failed = await CreateOperation("docker", "web-2");
        await FailOperation(failed.Id);
        await CreateOperation("transfer", "web-2");

        var byKind = await ReadPage($"{ListPath}?kind=transfer", session);
        var byStatus = await ReadPage($"{ListPath}?status=failed", session);
        var byServer = await ReadPage($"{ListPath}?server=web-2&limit=1", session);

        byKind.GetProperty("total").GetInt32().Should().Be(2);
        byStatus.GetProperty("items").GetArrayLength().Should().Be(1);
        byStatus.GetProperty("items")[0].GetProperty("status").GetString().Should().Be("failed");
        byServer.GetProperty("total").GetInt32().Should().Be(2);
        byServer.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [TestMethod]
    public async Task List_filters_by_time_range()
    {
        var session = await Authenticate();
        await CreateOperation("transfer", null);
        var future = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        var past = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));

        var afterNow = await ReadPage($"{ListPath}?from={future}", session);
        var withinRange = await ReadPage($"{ListPath}?from={past}&to={future}", session);

        afterNow.GetProperty("total").GetInt32().Should().Be(0);
        withinRange.GetProperty("total").GetInt32().Should().Be(1);
    }

    [TestMethod]
    public async Task List_with_unknown_status_is_bad_request()
    {
        var session = await Authenticate();

        using var response = await TestRequest.Get($"{ListPath}?status=bogus")
            .WithSession(session)
            .SendAsync(_client, TestContext.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Get_returns_operation_and_not_found_for_unknown_id()
    {
        var session = await Authenticate();
        var created = await CreateOperation("transfer", "web-1");

        using var found = await TestRequest.Get($"{ListPath}/{created.Id}").WithSession(session).SendAsync(_client, TestContext.CancellationToken);
        using var missing = await TestRequest.Get($"{ListPath}/{Guid.NewGuid()}").WithSession(session).SendAsync(_client, TestContext.CancellationToken);

        found.StatusCode.Should().Be(HttpStatusCode.OK);
        missing.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task Retry_of_failed_retryable_operation_calls_handler()
    {
        var session = await Authenticate();
        var created = await CreateOperation("transfer", "web-1", true);
        await FailOperation(created.Id);

        using var response = await SendRetry(session, created.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        await _retryHandler.Received(1).Retry(Arg.Is<Operation>(operation => operation.Id == created.Id), Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task Retry_without_handler_returns_clear_error()
    {
        var session = await Authenticate();
        var created = await CreateOperation("docker", "web-1", true);
        await FailOperation(created.Id);

        using var response = await SendRetry(session, created.Id);

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
        var body = await response.Content.ReadAsStringAsync(TestContext.CancellationToken);
        body.Should().Contain("NoRetryHandler").And.Contain("docker");
    }

    [TestMethod]
    public async Task Retry_of_non_retryable_operation_is_conflict()
    {
        var session = await Authenticate();
        var created = await CreateOperation("transfer", "web-1", false);
        await FailOperation(created.Id);

        using var response = await SendRetry(session, created.Id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task Retry_of_unknown_operation_is_not_found()
    {
        var session = await Authenticate();

        using var response = await SendRetry(session, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<AuthenticatedSession> Authenticate()
    {
        return await SessionBootstrapper.AuthenticateAsync(_factory, _client, TestContext.CancellationToken);
    }

    private Task<HttpResponseMessage> SendRetry(AuthenticatedSession session, Guid id)
    {
        return TestRequest.Post($"{ListPath}/{id}/retry")
            .WithSession(session)
            .WithOrigin(TermForgeApplicationFactory.ApplicationOrigin)
            .WithCsrfToken(session.CsrfToken)
            .SendAsync(_client, TestContext.CancellationToken);
    }

    private async Task<JsonElement> ReadPage(string path, AuthenticatedSession session)
    {
        using var response = await TestRequest.Get(path).WithSession(session).SendAsync(_client, TestContext.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.CancellationToken);
    }

    private async Task<Operation> CreateOperation(string kind, string? server, bool retryable = false)
    {
        using var scope = _factory.Services.CreateScope();
        var journal = scope.ServiceProvider.GetRequiredService<IOperationJournal>();
        var request = new CreateOperationRequest(kind, server, null, retryable);
        return await journal.Create(request, TestContext.CancellationToken);
    }

    private async Task FailOperation(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var journal = scope.ServiceProvider.GetRequiredService<IOperationJournal>();
        await journal.Start(id, TestContext.CancellationToken);
        await journal.Fail(id, "boom", TestContext.CancellationToken);
    }
}
