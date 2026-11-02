using DTech.Core.Files;
using DTech.Ssh.Files;
using FluentAssertions;
using NSubstitute;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class SftpDirectoryReaderTests
{
    private const string DirectoryPath = "/data";

    [TestMethod]
    public async Task List_WithoutLinkTargets_DoesNotRequestAttributesForAnyLink()
    {
        var client = Substitute.For<ISftpClient>();
        var files = new List<ISftpFile>();
        for (var i = 0; i < 128; i++)
        {
            var name = $"link-{i}";
            var file = Entry(name, true);
            files.Add(file);
        }

        client.ListDirectoryAsync(DirectoryPath, Arg.Any<CancellationToken>()).Returns(_ => Enumerate(files));
        var reader = new SftpDirectoryReader(client);

        var entries = await reader.ListAsync(DirectoryPath, false, CancellationToken.None);

        entries.Select(entry => entry.Name).Should().Equal(files.Select(file => file.Name));
        entries.Should().OnlyContain(entry => entry.Kind == RemoteFileKind.SymbolicLink && entry.LinkTargetKind == null && !entry.IsBrokenLink);
        await client.DidNotReceiveWithAnyArgs().GetAttributesAsync(default!, default);
    }

    [TestMethod]
    public async Task List_WithLinkTargets_LimitsConcurrentRequestsToEightAndPreservesOrder()
    {
        var client = Substitute.For<ISftpClient>();
        var files = new List<ISftpFile>();
        for (var i = 0; i < 32; i++)
        {
            var name = $"link-{i}";
            var file = Entry(name, true);
            files.Add(file);
        }

        client.ListDirectoryAsync(DirectoryPath, Arg.Any<CancellationToken>()).Returns(_ => Enumerate(files));
        var gate = new Lock();
        var full = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = 0;
        var maximum = 0;
        client.GetAttributesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var cancellationToken = call.ArgAt<CancellationToken>(1);
            return ResolveAsync(cancellationToken);
        });
        var reader = new SftpDirectoryReader(client);
        var reading = reader.ListAsync(DirectoryPath, true, CancellationToken.None);

        try
        {
            await full.Task.WaitAsync(TimeSpan.FromSeconds(10));
            lock (gate)
            {
                active.Should().Be(8);
                maximum.Should().Be(8);
            }
        }
        finally
        {
            release.TrySetResult();
        }

        var entries = await reading.WaitAsync(TimeSpan.FromSeconds(10));
        entries.Select(entry => entry.Name).Should().Equal(files.Select(file => file.Name));
        entries.Should().OnlyContain(entry => entry.Kind == RemoteFileKind.SymbolicLink && entry.IsBrokenLink);
        maximum.Should().Be(8);
        await client.Received(32).GetAttributesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

        async Task<SftpFileAttributes> ResolveAsync(CancellationToken cancellationToken)
        {
            lock (gate)
            {
                active++;
                maximum = Math.Max(maximum, active);
                if (active == 8)
                {
                    full.TrySetResult();
                }
            }

            try
            {
                await release.Task.WaitAsync(cancellationToken);
                throw new SftpPathNotFoundException("The link target is missing.");
            }
            finally
            {
                lock (gate)
                {
                    active--;
                }
            }
        }
    }

    [TestMethod]
    public async Task List_WithLinkTargets_SkipsAttributesForRegularFilesAndDotEntries()
    {
        var client = Substitute.For<ISftpClient>();
        ISftpFile[] files = [Entry(".", false), Entry("..", false), Entry("document", false)];
        client.ListDirectoryAsync(DirectoryPath, Arg.Any<CancellationToken>()).Returns(_ => Enumerate(files));
        var reader = new SftpDirectoryReader(client);

        var entries = await reader.ListAsync(DirectoryPath, true, CancellationToken.None);

        entries.Should().ContainSingle().Which.Name.Should().Be("document");
        await client.DidNotReceiveWithAnyArgs().GetAttributesAsync(default!, default);
    }

    private static ISftpFile Entry(string name, bool symbolicLink)
    {
        var file = Substitute.For<ISftpFile>();
        file.Name.Returns(name);
        file.FullName.Returns($"{DirectoryPath}/{name}");
        file.IsSymbolicLink.Returns(symbolicLink);
        file.IsRegularFile.Returns(!symbolicLink);
        return file;
    }

    private static async IAsyncEnumerable<ISftpFile> Enumerate(IReadOnlyList<ISftpFile> files)
    {
        for (var i = 0; i < files.Count; i++)
        {
            yield return files[i];
        }

        await Task.CompletedTask;
    }
}
