using DTech.Entrypoint.Lifecycle;
using DTech.Entrypoint.Security;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DTech.Entrypoint.Tests.Infrastructure;

internal sealed class TermForgeApplicationFactory : WebApplicationFactory<Program>
{
    public const string ApplicationOrigin = "http://localhost";

    private static readonly Dictionary<string, string?> NoSettings = new();

    private readonly IReadOnlyDictionary<string, string?> _settings;
    private readonly string _databaseDirectory = Path.Combine(Path.GetTempPath(), $"termforge-entrypoint-tests-{Guid.NewGuid():N}");

    public TermForgeApplicationFactory()
        : this(NoSettings)
    {
    }

    public TermForgeApplicationFactory(IReadOnlyDictionary<string, string?> settings)
    {
        _settings = settings;
    }

    public string? WebRootPath { get; init; }

    public IBrowserLauncher BrowserLauncher { get; } = Substitute.For<IBrowserLauncher>();

    public CapturingLoggerProvider Logs { get; } = new();

    public Action<IServiceCollection>? ConfigureServices { get; init; }

    public string BootstrapTokenValue => Services.GetRequiredService<BootstrapToken>().Value;

    public CancellationToken ApplicationStopping => Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

    public HttpClient CreateClientWithoutCookies()
    {
        var clientOptions = new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        };
        return CreateClient(clientOptions);
    }

    public HttpClient CreateClientFor(string baseAddress)
    {
        var baseAddressUri = new Uri(baseAddress);
        var clientOptions = new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = baseAddressUri,
        };
        return CreateClient(clientOptions);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Lifecycle:OpenBrowser", "false");
        var databasePath = Path.Combine(_databaseDirectory, "termforge.db");
        builder.UseSetting("Persistence:DatabasePath", databasePath);
        foreach (var setting in _settings)
        {
            builder.UseSetting(setting.Key, setting.Value);
        }

        builder.ConfigureLogging(ConfigureLogging);
        builder.ConfigureTestServices(ConfigureTestServices);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
        {
            return;
        }

        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_databaseDirectory))
        {
            Directory.Delete(_databaseDirectory, true);
        }
    }

    private void ConfigureLogging(ILoggingBuilder logging)
    {
        logging.AddProvider(Logs);
        logging.AddFilter<CapturingLoggerProvider>(null, LogLevel.Trace);
    }

    private void ConfigureTestServices(IServiceCollection services)
    {
        services.RemoveAll<IBrowserLauncher>();
        services.AddSingleton(BrowserLauncher);
        services.AddTransient<IStartupFilter, WebSocketProbeStartupFilter>();
        UseWebRootOverride(services);
        ConfigureServices?.Invoke(services);
    }

    private void UseWebRootOverride(IServiceCollection services)
    {
        if (WebRootPath is null)
        {
            return;
        }

        var descriptor = services.First(service => service.ServiceType == typeof(IWebHostEnvironment) && service.ImplementationInstance is not null);
        var environment = (IWebHostEnvironment)descriptor.ImplementationInstance!;
        environment.WebRootPath = WebRootPath;
        environment.WebRootFileProvider = new PhysicalFileProvider(WebRootPath);
    }
}
