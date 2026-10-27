using DTech.Core.Credentials;
using DTech.Credentials.Selection;
using DTech.Credentials.Tests.Support;
using DTech.Credentials.Vault;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DTech.Credentials.Tests;

[TestClass]
public sealed class CredentialStoreServiceCollectionExtensionsTests
{
    [TestMethod]
    public void AddCredentialStore_RegistersCoreAbstractionsAsSingletons()
    {
        using var directory = new TemporaryDirectory();
        var vaultPath = directory.Combine("configured.vault");
        using var provider = BuildProvider(vaultPath);

        var store = provider.GetRequiredService<ICredentialStore>();
        var vault = provider.GetRequiredService<ICredentialVault>();
        var statusProvider = provider.GetRequiredService<ICredentialStoreStatusProvider>();
        var sameStore = provider.GetRequiredService<ICredentialStore>();

        store.Should().BeOfType<CredentialStore>().And.BeSameAs(sameStore);
        vault.Should().BeOfType<CredentialVaultGate>();
        statusProvider.Should().BeOfType<CredentialStoreStatusProvider>();
    }

    [TestMethod]
    public void AddCredentialStore_BindsOptionsFromCredentialsSection()
    {
        using var directory = new TemporaryDirectory();
        var vaultPath = directory.Combine("configured.vault");
        using var provider = BuildProvider(vaultPath);

        var options = provider.GetRequiredService<IOptions<CredentialStoreOptions>>().Value;
        var vaultFile = provider.GetRequiredService<VaultFile>();

        options.VaultKdfIterations.Should().Be(2_000);
        vaultFile.FilePath.Should().Be(vaultPath);
    }

    [TestMethod]
    public void CredentialStoreOptions_DefaultsToStrongKeyDerivation()
    {
        var options = new CredentialStoreOptions();

        options.VaultKdfIterations.Should().BeGreaterThanOrEqualTo(600_000);
    }

    private static ServiceProvider BuildProvider(string vaultPath)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Credentials:VaultPath"] = vaultPath,
            ["Credentials:VaultKdfIterations"] = "2000",
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCredentialStore(configuration);
        return services.BuildServiceProvider();
    }
}
