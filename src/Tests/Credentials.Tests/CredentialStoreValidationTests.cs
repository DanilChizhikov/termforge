using DTech.Credentials.Native;
using DTech.Credentials.Native.Processes;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DTech.Credentials.Tests;

[TestClass]
public sealed class CredentialStoreValidationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("VaultKdfIterations", "1")]
    [DataRow("VaultKdfIterations", "10000001")]
    [DataRow("ProbeTimeout", "00:00:00")]
    [DataRow("ProbeTimeout", "00:01:01")]
    [DataRow("OperationTimeout", "-00:00:01")]
    [DataRow("OperationTimeout", "00:01:01")]
    public async Task InvalidCredentialOptions_PreventHostStartup(string property, string value)
    {
        var settings = new Dictionary<string, string?> { [$"Credentials:{property}"] = value };
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddCredentialStore(builder.Configuration);
        using var host = builder.Build();

        var starting = () => host.StartAsync(TestContext.CancellationToken);

        await starting.Should().ThrowAsync<OptionsValidationException>().WithMessage($"*Credentials:{property}*");
    }

    [TestMethod]
    public async Task ValidOptions_RegisterBoundedOperatingSystemStore()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Credentials:VaultKdfIterations"] = "600001",
            ["Credentials:ProbeTimeout"] = "00:00:02",
            ["Credentials:OperationTimeout"] = "00:00:03",
        };
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddCredentialStore(builder.Configuration);
        using var host = builder.Build();

        await host.StartAsync(TestContext.CancellationToken);
        var store = host.Services.GetRequiredService<IOperatingSystemCredentialStore>();

        store.Should().BeOfType<ProcessCredentialStore>();
        await host.StopAsync(TestContext.CancellationToken);
    }
}
