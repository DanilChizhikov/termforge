using DTech.Ssh.Terminal;
using FluentAssertions;
using Renci.SshNet.Common;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class OutputPumpTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FullQueue_StopsTheSourceWithoutWaitingForAConsumer()
    {
        var content = new byte[2 * 1024 * 1024];
        using var source = new MemoryStream(content);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pump = new ShellOutputPump(source, () => stopped.TrySetResult());

        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        await pump.StopAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        pump.Failure.Should().BeOfType<IOException>().Which.Message.Should().Contain("output backlog");
    }

    [TestMethod]
    public async Task Stop_UnblocksAnEmptySourceRead()
    {
        using var source = new PipeStream();
        var pump = new ShellOutputPump(source, () => Assert.Fail("Unexpected overflow"));

        await pump.StopAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.CancellationToken);

        pump.Failure.Should().BeNull();
    }

    [TestMethod]
    public async Task ReadingSmallBuffers_PreservesEveryByteInOrder()
    {
        var expected = new byte[128 * 1024];
        Random.Shared.NextBytes(expected);
        using var source = new MemoryStream(expected);
        var pump = new ShellOutputPump(source, () => Assert.Fail("Unexpected overflow"));
        using var received = new MemoryStream();
        var buffer = new byte[113];

        var read = await pump.ReadAsync(buffer, TestContext.CancellationToken);
        while (read > 0)
        {
            received.Write(buffer, 0, read);
            read = await pump.ReadAsync(buffer, TestContext.CancellationToken);
        }

        await pump.StopAsync();
        received.ToArray().Should().Equal(expected);
        pump.Failure.Should().BeNull();
    }
}
