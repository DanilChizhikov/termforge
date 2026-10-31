using System.Text;
using DTech.Entrypoint.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Security;

[TestClass]
public sealed class RequestRejectionTests
{
    [TestMethod]
    public async Task Rejection_writes_status_content_type_and_reason()
    {
        var context = new DefaultHttpContext();
        using var body = new MemoryStream();
        context.Response.Body = body;

        await RequestRejection.WriteAsync(context, StatusCodes.Status401Unauthorized, "A valid browser session is required");

        context.Response.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
        context.Response.ContentType.Should().Be("text/plain; charset=utf-8");
        Encoding.UTF8.GetString(body.ToArray()).Should().Be("A valid browser session is required");
    }

    [TestMethod]
    public async Task Request_canceled_before_writing_completes_without_exception()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        var responseBody = Substitute.For<IHttpResponseBodyFeature>();
        context.Features.Set(responseBody);
        responseBody.StartAsync(cancellation.Token).Returns(Task.FromCanceled(cancellation.Token));

        await RequestRejection.WriteAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized");

        _ = responseBody.Received(1).StartAsync(cancellation.Token);
    }

    [TestMethod]
    public async Task Request_canceled_during_writing_completes_without_exception()
    {
        using var cancellation = new CancellationTokenSource();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        var responseBody = Substitute.For<IHttpResponseBodyFeature>();
        context.Features.Set(responseBody);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        responseBody.StartAsync(cancellation.Token).Returns(_ =>
        {
            started.SetResult();
            return completion.Task;
        });

        var write = RequestRejection.WriteAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized");
        await started.Task;
        cancellation.Cancel();
        completion.SetCanceled(cancellation.Token);
        await write;

        _ = responseBody.Received(1).StartAsync(cancellation.Token);
    }

    [TestMethod]
    public async Task Cancellation_without_request_abort_is_propagated()
    {
        var context = new DefaultHttpContext();
        var responseBody = Substitute.For<IHttpResponseBodyFeature>();
        context.Features.Set(responseBody);
        var exception = new OperationCanceledException();
        responseBody.StartAsync(context.RequestAborted).Returns(Task.FromException(exception));

        var write = () => RequestRejection.WriteAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized");

        (await write.Should().ThrowAsync<OperationCanceledException>()).Which.Should().BeSameAs(exception);
    }

    [TestMethod]
    public async Task Other_errors_are_propagated_even_when_request_is_canceled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        var responseBody = Substitute.For<IHttpResponseBodyFeature>();
        context.Features.Set(responseBody);
        var exception = new IOException("Response write failed");
        responseBody.StartAsync(cancellation.Token).Returns(Task.FromException(exception));

        var write = () => RequestRejection.WriteAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized");

        (await write.Should().ThrowAsync<IOException>()).Which.Should().BeSameAs(exception);
    }
}
