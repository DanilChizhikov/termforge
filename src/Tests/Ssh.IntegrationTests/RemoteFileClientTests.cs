using DTech.Core.Connections;
using DTech.Core.Files;
using DTech.Ssh.IntegrationTests.Harness;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace DTech.Ssh.IntegrationTests;

[TestClass]
public sealed class RemoteFileClientTests
{
    private const int PayloadSize = 256 * 1024;

    [TestMethod]
    public async Task UploadListDownload_RoundTripsFileContent()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        var target = cluster.Target.CreateKeyTarget();
        await using var client = await ConnectAsync(services, target);
        var payload = new byte[PayloadSize];
        Random.Shared.NextBytes(payload);
        var fileName = $"upload-{Guid.NewGuid():N}.bin";
        var remotePath = $"{SshServerContainer.HomeDirectory}/{fileName}";

        using var source = new MemoryStream(payload);
        var uploadResult = await client.UploadAsync(source, remotePath, CancellationToken.None);
        var listResult = await client.ListAsync(SshServerContainer.HomeDirectory, CancellationToken.None);
        using var destination = new MemoryStream();
        var downloadResult = await client.DownloadAsync(remotePath, destination, CancellationToken.None);

        uploadResult.IsSuccess.Should().BeTrue(uploadResult.Error?.Message);
        listResult.IsSuccess.Should().BeTrue(listResult.Error?.Message);
        listResult.Value.Should().ContainSingle(entry => entry.Name == fileName)
            .Which.Should().Match<RemoteFileEntry>(entry => entry.Kind == RemoteFileKind.File && entry.Size == PayloadSize && entry.FullPath == remotePath);
        listResult.Value.Should().NotContain(entry => entry.Name == "." || entry.Name == "..");
        downloadResult.IsSuccess.Should().BeTrue(downloadResult.Error?.Message);
        var downloaded = destination.ToArray();
        downloaded.Should().Equal(payload);
    }

    [TestMethod]
    public async Task List_ShowsDirectoriesWithKind()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        var target = cluster.Target.CreateKeyTarget();
        await using var client = await ConnectAsync(services, target);

        var listResult = await client.ListAsync("/", CancellationToken.None);

        listResult.IsSuccess.Should().BeTrue(listResult.Error?.Message);
        listResult.Value.Should().Contain(entry => entry.Name == "home" && entry.Kind == RemoteFileKind.Directory);
    }

    [TestMethod]
    public async Task Download_MissingFile_ReturnsRemotePathNotFound()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        var target = cluster.Target.CreateKeyTarget();
        await using var client = await ConnectAsync(services, target);
        using var destination = new MemoryStream();

        var missingPath = $"{SshServerContainer.HomeDirectory}/missing-file";

        var result = await client.DownloadAsync(missingPath, destination, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.RemotePathNotFound);
    }

    [TestMethod]
    public async Task List_MissingDirectory_ReturnsRemotePathNotFound()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        var target = cluster.Target.CreateKeyTarget();
        await using var client = await ConnectAsync(services, target);

        var result = await client.ListAsync("/missing-directory", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.RemotePathNotFound);
    }

    [TestMethod]
    public async Task Upload_IntoReadOnlyDirectory_ReturnsPermissionDenied()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        var target = cluster.Target.CreateKeyTarget();
        await using var client = await ConnectAsync(services, target);
        var content = "forbidden"u8.ToArray();
        using var source = new MemoryStream(content);

        var result = await client.UploadAsync(source, "/etc/termforge-forbidden.txt", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.PermissionDenied);
    }

    [TestMethod]
    public async Task Download_WhenCancelled_ReturnsCancelled()
    {
        var cluster = await SshTestEnvironment.RequireClusterAsync();
        await using var services = CreateServices(cluster);
        var target = cluster.Target.CreateKeyTarget();
        await using var client = await ConnectAsync(services, target);
        using var destination = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await client.DownloadAsync("/etc/os-release", destination, cancellation.Token);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Kind.Should().Be(SshErrorKind.Cancelled);
    }

    private static ServiceProvider CreateServices(SshServerCluster cluster)
    {
        var verifier = SshTransportTestServices.CreatePinnedVerifier(cluster.Target.Identity.HostKey);
        return SshTransportTestServices.Create(verifier);
    }

    private static async Task<IRemoteFileClient> ConnectAsync(ServiceProvider services, SshConnectionTarget target)
    {
        var factory = services.GetRequiredService<IRemoteFileClientFactory>();
        var result = await factory.ConnectAsync(target, CancellationToken.None);
        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        return result.Value;
    }
}
