using System.ComponentModel;
using DTech.Entrypoint.Lifecycle;
using DTech.Entrypoint.Security;
using DTech.Entrypoint.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace DTech.Entrypoint.Tests.Lifecycle;

[TestClass]
public sealed class BrowserOpeningServiceTests
{
    private readonly CancellationTokenSource _started = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly IBrowserLauncher _browserLauncher = Substitute.For<IBrowserLauncher>();
    private readonly CapturingLoggerProvider _logs = new();
    private readonly BootstrapToken _bootstrapToken = new();
    private readonly ServerAddressesFeature _serverAddresses = new();

    public TestContext TestContext { get; set; } = null!;

    [TestCleanup]
    public void Cleanup()
    {
        _started.Dispose();
        _stopping.Dispose();
    }

    [TestMethod]
    public async Task Browser_opens_bootstrap_url_after_application_started()
    {
        _serverAddresses.Addresses.Add("http://127.0.0.1:5023");
        var service = CreateService(true);

        await service.StartAsync(TestContext.CancellationToken);
        _browserLauncher.DidNotReceiveWithAnyArgs().Open(default!);
        await _started.CancelAsync();

        var expectedUrl = $"http://127.0.0.1:5023/bootstrap#token={_bootstrapToken.Value}";
        var expectedAddress = Arg.Is<Uri>(address => address.AbsoluteUri == expectedUrl);
        _browserLauncher.Received(1).Open(expectedAddress);
        _logs.Messages.Should().NotContain(message => message.Contains(_bootstrapToken.Value));
    }

    [TestMethod]
    public async Task Browser_is_not_opened_when_disabled()
    {
        _serverAddresses.Addresses.Add("http://127.0.0.1:5023");
        var service = CreateService(false);

        await service.StartAsync(TestContext.CancellationToken);
        await _started.CancelAsync();

        _browserLauncher.DidNotReceiveWithAnyArgs().Open(default!);
    }

    [TestMethod]
    public async Task Browser_is_not_opened_without_loopback_address()
    {
        _serverAddresses.Addresses.Add("http://192.168.1.10:5023");
        var service = CreateService(true);

        await service.StartAsync(TestContext.CancellationToken);
        await _started.CancelAsync();

        _browserLauncher.DidNotReceiveWithAnyArgs().Open(default!);
    }

    [TestMethod]
    public async Task Browser_launch_failure_is_logged_without_bootstrap_token()
    {
        _serverAddresses.Addresses.Add("http://127.0.0.1:5023");
        var launchFailure = new Win32Exception(2, $"Cannot start http://127.0.0.1:5023/bootstrap#token={_bootstrapToken.Value}");
        _browserLauncher.WhenForAnyArgs(launcher => launcher.Open(default!)).Throw(launchFailure);
        var service = CreateService(true);

        await service.StartAsync(TestContext.CancellationToken);
        var startedNotification = () => _started.Cancel();

        startedNotification.Should().NotThrow();
        _logs.Messages.Should().ContainSingle(message => message.Contains("could not be opened"));
        _logs.Messages.Should().NotContain(message => message.Contains(_bootstrapToken.Value));
    }

    private BrowserOpeningService CreateService(bool openBrowser)
    {
        var features = new FeatureCollection();
        features.Set<IServerAddressesFeature>(_serverAddresses);
        var server = Substitute.For<IServer>();
        server.Features.Returns(features);

        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStarted.Returns(_started.Token);
        lifetime.ApplicationStopping.Returns(_stopping.Token);

        var lifecycleOptions = new LifecycleOptions { OpenBrowser = openBrowser };
        var options = Options.Create(lifecycleOptions);
        var bootstrapUrlFactory = new BootstrapUrlFactory(server, _bootstrapToken);
        var loggerFactory = LoggerFactory.Create(ConfigureLogging);
        var logger = loggerFactory.CreateLogger<BrowserOpeningService>();
        return new BrowserOpeningService(bootstrapUrlFactory, _browserLauncher, options, lifetime, logger);
    }

    private void ConfigureLogging(ILoggingBuilder logging)
    {
        logging.AddProvider(_logs);
        logging.SetMinimumLevel(LogLevel.Trace);
    }
}
