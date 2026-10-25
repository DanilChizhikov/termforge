using System.Security.Cryptography;
using DTech.Core.Credentials;
using Microsoft.Extensions.Options;

namespace DTech.Credentials.Native;

public sealed class OperatingSystemCredentialStoreProbe(IOperatingSystemCredentialStore store, IOptions<CredentialStoreOptions> options)
{
    public const string ProbeKeyValue = "termforge-availability-probe";

    private const int ProbeSecretLength = 32;

    public async Task<CredentialStoreAvailability> Probe(CancellationToken cancellationToken)
    {
        var prerequisites = store.CheckPrerequisites();
        if (!prerequisites.IsAvailable)
        {
            return prerequisites;
        }

        var timeout = options.Value.ProbeTimeout;
        var roundTrip = Task.Run(RunRoundTrip, cancellationToken);
        try
        {
            return await roundTrip.WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            return CredentialStoreAvailability.Unavailable(store.Name, $"{store.Name} did not respond within {timeout.TotalSeconds:0.#} seconds. It may be locked or waiting for a confirmation dialog.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CredentialStoreAvailability.Unavailable(store.Name, $"{store.Name} could not be used ({exception.GetType().Name}): {exception.Message}");
        }
    }

    private async Task<CredentialStoreAvailability> RunRoundTrip()
    {
        var keyResult = CredentialKey.Create(ProbeKeyValue);
        var key = keyResult.Value;
        var probeText = RandomNumberGenerator.GetHexString(ProbeSecretLength);
        using var probeSecret = Secret.FromString(probeText);

        var storeResult = await store.Store(key, probeSecret, CancellationToken.None);
        if (!storeResult.IsSuccess)
        {
            return Unavailable("store", storeResult.Error);
        }

        var readResult = await store.Read(key, CancellationToken.None);
        if (!readResult.IsSuccess)
        {
            return Unavailable("read", readResult.Error);
        }

        using var readSecret = readResult.Value;
        if (!readSecret.ContentEquals(probeSecret))
        {
            return CredentialStoreAvailability.Unavailable(store.Name, $"{store.Name} returned a different value than the one stored during the availability self-test.");
        }

        var deleteResult = await store.Delete(key, CancellationToken.None);
        if (!deleteResult.IsSuccess)
        {
            return Unavailable("delete", deleteResult.Error);
        }

        return CredentialStoreAvailability.Available(store.Name);
    }

    private CredentialStoreAvailability Unavailable(string operation, CredentialError error)
    {
        return CredentialStoreAvailability.Unavailable(store.Name, $"{store.Name} failed the availability self-test ({operation}): {error.Message}");
    }
}
