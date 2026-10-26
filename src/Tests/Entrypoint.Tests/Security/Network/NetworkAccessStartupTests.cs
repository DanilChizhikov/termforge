using DTech.Entrypoint.Security;
using DTech.Entrypoint.Security.Network;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DTech.Entrypoint.Tests.Security.Network;

[TestClass]
public sealed class NetworkAccessStartupTests
{
    private const string KestrelEndpointUrlKey = "Kestrel:Endpoints:Http:Url";

    private readonly CapturingLoggerProvider _logs = new();

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task Non_loopback_binding_is_rejected_when_network_access_is_not_enabled()
    {
        var settings = NetworkAccessSettings.CreateEnabled();
        settings.Remove("NetworkAccess:Enabled");
        await using var app = BuildSecuredApplication(settings);

        var start = () => app.StartAsync(TestContext.CancellationToken);

        await start.Should().ThrowAsync<InsecureBindingException>().WithMessage("*NetworkAccess:Enabled is false*");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("not-a-hash")]
    public async Task Network_mode_is_refused_without_configured_password(string? passwordHash)
    {
        var settings = NetworkAccessSettings.CreateEnabled();
        settings["NetworkAccess:PasswordHash"] = passwordHash;
        await using var app = BuildSecuredApplication(settings);

        var start = () => app.StartAsync(TestContext.CancellationToken);

        await start.Should().ThrowAsync<NetworkAccessConfigurationException>().WithMessage("*NetworkAccess:PasswordHash*");
    }

    [TestMethod]
    public async Task Network_mode_is_refused_without_password_even_on_loopback_binding()
    {
        var settings = new Dictionary<string, string?>
        {
            [KestrelEndpointUrlKey] = "http://127.0.0.1:5023",
            ["NetworkAccess:Enabled"] = "true",
            ["NetworkAccess:AllowedHosts:0"] = NetworkAccessSettings.AllowedHost,
        };
        await using var app = BuildSecuredApplication(settings);

        var start = () => app.StartAsync(TestContext.CancellationToken);

        await start.Should().ThrowAsync<NetworkAccessConfigurationException>().WithMessage("*refuses to start*PasswordHash*");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("*")]
    [DataRow("https://termforge.lan")]
    [DataRow("termforge.lan:5023")]
    public async Task Network_mode_is_refused_without_valid_allowed_hosts(string? allowedHost)
    {
        var settings = NetworkAccessSettings.CreateEnabled();
        settings["NetworkAccess:AllowedHosts:0"] = allowedHost;
        await using var app = BuildSecuredApplication(settings);

        var start = () => app.StartAsync(TestContext.CancellationToken);

        await start.Should().ThrowAsync<NetworkAccessConfigurationException>().WithMessage("*NetworkAccess:AllowedHosts*");
    }

    [TestMethod]
    public async Task Network_mode_is_refused_on_plain_http_without_explicit_opt_in()
    {
        var settings = NetworkAccessSettings.CreateEnabled();
        settings[KestrelEndpointUrlKey] = "http://0.0.0.0:5023";
        await using var app = BuildSecuredApplication(settings);

        var start = () => app.StartAsync(TestContext.CancellationToken);

        await start.Should().ThrowAsync<NetworkAccessConfigurationException>().WithMessage("*plain HTTP*AllowInsecureHttp*");
    }

    [TestMethod]
    public async Task Network_mode_starts_on_https_with_password_and_allowed_hosts()
    {
        var settings = NetworkAccessSettings.CreateEnabled();
        await using var app = BuildSecuredApplication(settings);

        var start = () => app.StartAsync(TestContext.CancellationToken);

        await start.Should().NotThrowAsync();
        _logs.Messages.Should().Contain(message => message.Contains("Network access mode is enabled"));
        _logs.Messages.Should().NotContain(message => message.Contains("INSECURE"));
        await app.StopAsync(TestContext.CancellationToken);
    }

    [TestMethod]
    public async Task Network_mode_on_plain_http_with_opt_in_starts_with_loud_warning()
    {
        var settings = NetworkAccessSettings.CreateEnabled();
        settings[KestrelEndpointUrlKey] = "http://0.0.0.0:5023";
        settings["NetworkAccess:AllowInsecureHttp"] = "true";
        await using var app = BuildSecuredApplication(settings);

        var start = () => app.StartAsync(TestContext.CancellationToken);

        await start.Should().NotThrowAsync();
        _logs.Messages.Should().Contain(message => message.Contains("INSECURE") && message.Contains("http://0.0.0.0:5023"));
        _logs.Messages.Should().NotContain(message => message.Contains(NetworkAccessSettings.PasswordHash));
        await app.StopAsync(TestContext.CancellationToken);
    }

    private WebApplication BuildSecuredApplication(Dictionary<string, string?> settings)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.WebHost.UseTestServer();
        builder.Logging.AddProvider(_logs);
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.AddLocalHostSecurity();
        var app = builder.Build();
        app.UseLocalHostSecurity();
        return app;
    }
}
