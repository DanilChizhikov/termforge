using DTech.Entrypoint.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace DTech.Entrypoint.Tests.Security;

[TestClass]
public sealed class LoopbackBindingValidatorTests
{
    [TestMethod]
    [DataRow("http://127.0.0.1:5023")]
    [DataRow("http://127.0.0.1:0")]
    [DataRow("http://localhost:5023")]
    [DataRow("http://[::1]:5023")]
    [DataRow("https://127.0.0.1:5023")]
    [DataRow("http://127.0.0.1:5023;http://[::1]:5023")]
    public void Loopback_urls_are_accepted(string urls)
    {
        var configuration = CreateConfiguration("urls", urls);

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
        var configuration = CreateConfiguration("urls", urls);

        var violations = LoopbackBindingValidator.FindNonLoopbackBindings(configuration);

        violations.Should().ContainSingle();
    }

    [TestMethod]
    public void Non_loopback_kestrel_endpoint_is_rejected()
    {
        var configuration = CreateConfiguration("Kestrel:Endpoints:Lan:Url", "http://10.0.0.5:5023");

        var violations = LoopbackBindingValidator.FindNonLoopbackBindings(configuration);

        violations.Should().ContainSingle().Which.Should().Contain("10.0.0.5");
    }

    [TestMethod]
    [DataRow("http_ports")]
    [DataRow("https_ports")]
    public void Port_only_binding_is_rejected(string key)
    {
        var configuration = CreateConfiguration(key, "8080");

        var violations = LoopbackBindingValidator.FindNonLoopbackBindings(configuration);

        violations.Should().ContainSingle();
    }

    private static IConfiguration CreateConfiguration(string key, string value)
    {
        var settings = new Dictionary<string, string?> { [key] = value };
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }
}
