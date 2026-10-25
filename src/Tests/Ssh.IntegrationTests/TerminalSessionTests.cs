using System.Text.RegularExpressions;
using DTech.Core.Connections;
using DTech.Core.Terminal;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class TerminalSessionTests
{
    private const string PrintSizeCommand = "printf 'SIZE[%s]\\n' \"$(stty size)\"";

    private static readonly TerminalSize InitialSize = new(80, 24);
    private static readonly Regex SizePattern = new(@"SIZE\[(\d+) (\d+)\]");
    private static readonly Regex TtyPattern = new(@"TTY\[(/dev/pts/\d+)\]");
    private static readonly Regex TermPattern = new(@"TERM\[([a-z0-9-]+)\]");
    private static readonly Regex SumPattern = new(@"SUM\[(\d+)\]");

    [TestMethod]
    public async Task OpenAsync_AllocatesPtyAndExchangesInputAndOutput()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        await using var session = await OpenSessionAsync(services, cluster);
        var conversation = new TerminalConversation(session);

        await conversation.SendLineAsync("printf 'TTY[%s]\\n' \"$(tty)\"");
        var ttyMatch = await conversation.ExpectAsync(TtyPattern);
        await conversation.SendLineAsync("echo \"TERM[$TERM]\"");
        var termMatch = await conversation.ExpectAsync(TermPattern);
        await conversation.SendLineAsync("echo \"SUM[$((40 + 2))]\"");
        var sumMatch = await conversation.ExpectAsync(SumPattern);

        session.IsOpen.Should().BeTrue();
        ttyMatch.Groups[1].Value.Should().StartWith("/dev/pts/");
        termMatch.Groups[1].Value.Should().Be(TerminalSessionRequest.DefaultTerminalType);
        sumMatch.Groups[1].Value.Should().Be("42");
    }

    [TestMethod]
    public async Task Resize_ChangesRemotePtyWindowSize()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        await using var session = await OpenSessionAsync(services, cluster);
        var conversation = new TerminalConversation(session);

        await conversation.SendLineAsync(PrintSizeCommand);
        var initialSize = await conversation.ExpectAsync(SizePattern);
        var resizedTo = new TerminalSize(132, 40);
        var resizeResult = session.Resize(resizedTo);
        await conversation.SendLineAsync(PrintSizeCommand);
        var changedSize = await conversation.ExpectAsync(SizePattern);

        resizeResult.IsSuccess.Should().BeTrue(resizeResult.Error?.Message);
        initialSize.Groups[1].Value.Should().Be("24");
        initialSize.Groups[2].Value.Should().Be("80");
        changedSize.Groups[1].Value.Should().Be("40");
        changedSize.Groups[2].Value.Should().Be("132");
    }

    [TestMethod]
    public async Task Resize_WithInvalidSize_ReturnsErrorAndKeepsSessionOpen()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        await using var session = await OpenSessionAsync(services, cluster);
        var invalidSize = new TerminalSize(0, 10);

        var result = session.Resize(invalidSize);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.OperationFailed);
        session.IsOpen.Should().BeTrue();
    }

    [TestMethod]
    public async Task OpenAsync_TwiceGivesIndependentSessions()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        await using var firstSession = await OpenSessionAsync(services, cluster);
        await using var secondSession = await OpenSessionAsync(services, cluster);
        var firstConversation = new TerminalConversation(firstSession);
        var secondConversation = new TerminalConversation(secondSession);
        var secondSize = new TerminalSize(100, 30);

        var resizeResult = secondSession.Resize(secondSize);
        await firstConversation.SendLineAsync(PrintSizeCommand);
        await secondConversation.SendLineAsync(PrintSizeCommand);
        var firstReported = await firstConversation.ExpectAsync(SizePattern);
        var secondReported = await secondConversation.ExpectAsync(SizePattern);

        resizeResult.IsSuccess.Should().BeTrue(resizeResult.Error?.Message);
        firstReported.Value.Should().Be("SIZE[24 80]");
        secondReported.Value.Should().Be("SIZE[30 100]");
    }

    [TestMethod]
    public async Task RemoteExit_ClosesSessionAndRejectsFurtherInput()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        await using var session = await OpenSessionAsync(services, cluster);
        var conversation = new TerminalConversation(session);

        await conversation.SendLineAsync("exit");
        await conversation.WaitForCloseAsync();
        var lateInput = "echo late\n"u8.ToArray();
        var writeAfterClose = await session.WriteAsync(lateInput, CancellationToken.None);

        session.IsOpen.Should().BeFalse();
        writeAfterClose.IsSuccess.Should().BeFalse();
        writeAfterClose.Error!.Kind.Should().Be(SshErrorKind.SessionClosed);
    }

    private static ServiceProvider CreateServices(SshServerCluster cluster)
    {
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        return SshTransportTestServices.Create(verifier);
    }

    private static async Task<ITerminalSession> OpenSessionAsync(ServiceProvider services, SshServerCluster cluster)
    {
        var factory = services.GetRequiredService<ITerminalSessionFactory>();
        var target = cluster.Target.CreateKeyTarget();
        var request = new TerminalSessionRequest(target, InitialSize, TerminalSessionRequest.DefaultTerminalType);
        var opened = await factory.OpenAsync(request, CancellationToken.None);
        opened.IsSuccess.Should().BeTrue(opened.Error?.Message);
        return opened.Value;
    }
}
