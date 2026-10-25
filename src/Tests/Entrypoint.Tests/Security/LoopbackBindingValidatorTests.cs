using DTech.Entrypoint.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace DTech.Entrypoint.Tests.Security;

[TestClass]
public sealed class LoopbackBindingValidatorTests
{
    private const string KestrelEndpointUrlKey = "Kestrel:Endpoints:Http:Url";
    private const string LoopbackEndpoint = "http://127.0.0.1:5023";

    [TestMethod]
    [DataRow("http://127.0.0.1:5023")]
    [DataRow("http://127.0.0.1:0")]
    [DataRow("http://localhost:5023")]
    [DataRow("http://[::1]:5023")]
    [DataRow("https://127.0.0.1:5023")]
    [DataRow("http://127.0.0.1:5023;http://[::1]:5023")]
    public void Loopback_urls_are_accepted(string urls)
    {
        var configuration = CreateConfiguration(("urls", urls));

        var violations = LoopbackBindingValidator.FindNonLoopbackBindings(configuration);

        violations.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("http://0.0.0.0:5023")]
    [DataRow("http://*:5023")]
    [DataRow("http://+:5023")]
    [DataRow("http://[::]:5023")]
    [DataRow("http://192.168.1.10:5023")]
    [DataRow("http://my-machine:5023")]
    [DataRow("http://unix:/tmp/termforge.sock")]
    [DataRow("http://127.0.0.1:5023;http://0.0.0.0:5024")]
    public void Non_loopback_urls_are_rejected(string urls)
    {
        var configuration = CreateConfiguration(("urls", urls));

        var violations = LoopbackBindingValidator.FindNonLoopbackBindings(configuration);

        violations.Should().ContainSingle();
    }

    [TestMethod]
    public void Non_loopback_kestrel_endpoint_is_rejected()
    {
        var configuration = CreateConfiguration(("Kestrel:Endpoints:Lan:Url", "http://10.0.0.5:5023"));

        var violations = LoopbackBindingValidator.FindNonLoopbackBindings(configuration);

        violations.Should().ContainSingle().Which.Should().Contain("10.0.0.5");
    }

    [TestMethod]
    [DataRow("http_ports")]
    [DataRow("https_ports")]
    public void Port_only_binding_is_rejected(string key)
    {
        var configuration = CreateConfiguration((key, "8080"));

        var violations = LoopbackBindingValidator.FindNonLoopbackBindings(configuration);

        violations.Should().ContainSingle();
    }

    [TestMethod]
    [DataRow("urls", "http://0.0.0.0:8080")]
    [DataRow("http_ports", "8080")]
    [DataRow("https_ports", "8443")]
    public void Hosting_addresses_overridden_by_kestrel_endpoints_are_ignored(string key, string value)
    {
        var configuration = CreateConfiguration((KestrelEndpointUrlKey, LoopbackEndpoint), (key, value));

        var violations = LoopbackBindingValidator.FindNonLoopbackBindings(configuration);

        violations.Should().BeEmpty();
    }

    [TestMethod]
    [DataRow("urls", "http://0.0.0.0:8080")]
    [DataRow("http_ports", "8080")]
    public void Hosting_addresses_preferred_over_kestrel_endpoints_are_validated(string key, string value)
    {
        var configuration = CreateConfiguration((KestrelEndpointUrlKey, LoopbackEndpoint), (key, value), ("preferHostingUrls", "true"));

        var violations = LoopbackBindingValidator.FindNonLoopbackBindings(configuration);

        violations.Should().ContainSingle();
    }

    [TestMethod]
    public void Ports_are_ignored_when_urls_are_set()
    {
        var configuration = CreateConfiguration(("urls", LoopbackEndpoint), ("http_ports", "8080"));

        var violations = LoopbackBindingValidator.FindNonLoopbackBindings(configuration);

        violations.Should().BeEmpty();
    }

    private static IConfiguration CreateConfiguration(params (string Key, string Value)[] entries)
    {
        var settings = new Dictionary<string, string?>();
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            settings[entry.Key] = entry.Value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
