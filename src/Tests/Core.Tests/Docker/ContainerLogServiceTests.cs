using System.Text;
using DTech.Core.Connections;
using DTech.Core.Diagnostics;
using DTech.Core.Docker;
using DTech.Core.Tests.Docker.Support;
using FluentAssertions;
using NSubstitute;

namespace DTech.Core.Tests.Docker;

[TestClass]
public sealed class ContainerLogServiceTests
{
    private DockerTestGraph _graph = null!;

    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Initialize()
    {
        _graph = new DockerTestGraph();
    }

    [TestMethod]
    public async Task Open_StartsQuotedLogsCommandWithTailAndFollow()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        var recorded = new RecordedCommandStream([], 0);
        var started = SshResult<IRemoteCommandStream>.Success(recorded);
        _graph.StreamFactory.StartAsync(default!, default!, default).ReturnsForAnyArgs(started);
        var service = _graph.CreateLogService();
        var options = ContainerLogOptions.Create(50, true);
        var request = new ContainerLogRequest(connection.Id, "web", options);

        var result = await service.OpenAsync(request, TestContext.CancellationToken);

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        await _graph.StreamFactory.Received(1).StartAsync(
            Arg.Is<SshConnectionTarget>(target => target.Endpoint.Host == "web.example.test"),
            "docker logs --tail 50 --follow 'web' 2>&1",
            Arg.Any<CancellationToken>());
        await result.Value.DisposeAsync();
        recorded.Disposed.Should().BeTrue();
    }

    [TestMethod]
    public async Task Open_WithUnsafeContainer_IsRejectedWithoutStartingCommand()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        var service = _graph.CreateLogService();
        var options = ContainerLogOptions.Create(null, true);
        var request = new ContainerLogRequest(connection.Id, "web; cat /etc/shadow", options);

        var result = await service.OpenAsync(request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(DockerErrorCode.InvalidRequest);
        await _graph.StreamFactory.DidNotReceiveWithAnyArgs().StartAsync(default!, default!, default);
    }

    [TestMethod]
    public async Task Open_WhenSshFails_ReturnsConnectionFailed()
    {
        var connection = await _graph.AddConnectionAsync("web", TestContext.CancellationToken);
        var sshError = new SshError(SshErrorKind.AuthenticationFailed, "denied");
        var failed = SshResult<IRemoteCommandStream>.Failure(sshError);
        _graph.StreamFactory.StartAsync(default!, default!, default).ReturnsForAnyArgs(failed);
        var service = _graph.CreateLogService();
        var options = ContainerLogOptions.Create(null, false);
        var request = new ContainerLogRequest(connection.Id, "web", options);

        var result = await service.OpenAsync(request, TestContext.CancellationToken);

        result.Error!.Code.Should().Be(DockerErrorCode.ConnectionFailed);
        result.Error.SshErrorKind.Should().Be(SshErrorKind.AuthenticationFailed);
    }

    [TestMethod]
    public async Task LogStream_DecodesMultiByteCharactersSplitAcrossChunksAndReportsExitCode()
    {
        var bytes = Encoding.UTF8.GetBytes("ok ✓\n");
        byte[] first = bytes[..4];
        byte[] second = bytes[4..];
        var recorded = new RecordedCommandStream([first, second], 0);
        await using var stream = new ContainerLogStream(recorded);

        var text = new StringBuilder();
        var chunk = await stream.ReadAsync(TestContext.CancellationToken);
        while (chunk is not null)
        {
            text.Append(chunk);
            chunk = await stream.ReadAsync(TestContext.CancellationToken);
        }

        text.ToString().Should().Be("ok ✓\n");
        stream.ExitCode.Should().Be(0);
    }

    [TestMethod]
    public async Task LogStream_WhenCancelled_Throws()
    {
        var recorded = new RecordedCommandStream([[1, 2, 3]], 0);
        await using var stream = new ContainerLogStream(recorded);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var read = async () => await stream.ReadAsync(cancellation.Token);

        await read.Should().ThrowAsync<OperationCanceledException>();
    }
}
