using DTech.Entrypoint.Lifecycle;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Lifecycle;

[TestClass]
public sealed class ShutdownAdmissionTests
{
    [TestMethod]
    [DataRow("/api/ws", "GET", true)]
    [DataRow("/api/terminals/session/ws", "GET", true)]
    [DataRow("/api/docker/pull", "GET", true)]
    [DataRow("/mcp", "POST", false)]
    [DataRow("/mcp", "GET", false)]
    [DataRow("/mcp/session", "POST", false)]
    [DataRow("/api/connections", "POST", false)]
    public async Task Exiting_RejectsNewWorkBeforeCallingTheEndpoint(string path, string method, bool webSocket)
    {
        for (var i = 0; i < 2; i++)
        {
            var gate = new WorkAdmissionGate();
            if (i == 0)
            {
                gate.TryBeginWaiting().Should().BeTrue();
            }
            else
            {
                gate.Close();
            }

            await using var services = CreateServices();
            var context = CreateContext(services);
            context.Request.Path = path;
            context.Request.Method = method;
            var feature = Substitute.For<IHttpWebSocketFeature>();
            feature.IsWebSocketRequest.Returns(webSocket);
            context.Features.Set(feature);
            var called = false;
            var middleware = new WorkAdmissionMiddleware(_ =>
            {
                called = true;
                return Task.CompletedTask;
            }, gate);

            await middleware.InvokeAsync(context);

            called.Should().BeFalse();
            context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
            context.Response.Body.Position = 0;
            using var reader = new StreamReader(context.Response.Body);
            var problem = await reader.ReadToEndAsync();
            problem.Should().Contain(WorkAdmissionMiddleware.ExitingCode);
        }
    }

    [TestMethod]
    [DataRow("/api/lifecycle/exit", "DELETE")]
    [DataRow("/api/lifecycle/attention", "GET")]
    [DataRow("/api/session/logout", "POST")]
    [DataRow("/api/workspace", "PUT")]
    [DataRow("/api/transfers", "GET")]
    [DataRow("/api/transfers", "HEAD")]
    [DataRow("/api/transfers", "OPTIONS")]
    [DataRow("/", "GET")]
    public async Task Exiting_PreservesLifecycleAndWorkspaceRequests(string path, string method)
    {
        var gate = new WorkAdmissionGate();
        gate.Close();
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        var called = false;
        var middleware = new WorkAdmissionMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }, gate);

        await middleware.InvokeAsync(context);

        called.Should().BeTrue();
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateContext(ServiceProvider services)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        return context;
    }
}
