using System.Security.Cryptography;
using DTech.Core.Credentials;
using DTech.Credentials.Native.Processes;
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
        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var roundTrip = RunRoundTrip(linked.Token);
        try
        {
            return store is ProcessCredentialStore
                ? await roundTrip
                : await roundTrip.WaitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CredentialStoreAvailability.Unavailable(store.Name, $"{store.Name} did not respond within {timeout.TotalSeconds:0.#} seconds. It may be locked or waiting for a confirmation dialog.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CredentialStoreAvailability.Unavailable(store.Name, $"{store.Name} could not be used ({exception.GetType().Name}): {exception.Message}");
        }
    }

    private async Task<CredentialStoreAvailability> RunRoundTrip(CancellationToken cancellationToken)
    {
        var keyText = $"{ProbeKeyValue}-{Guid.NewGuid():N}";
        var keyResult = CredentialKey.Create(keyText);
        var key = keyResult.Value;
        var probeText = RandomNumberGenerator.GetHexString(ProbeSecretLength);
        using var probeSecret = Secret.FromString(probeText);

        var storeResult = await store.Store(key, probeSecret, cancellationToken);
        if (!storeResult.IsSuccess)
        {
            return Unavailable("store", storeResult.Error);
        }

        var deleted = false;
        try
        {
            var readResult = await store.Read(key, cancellationToken);
            if (!readResult.IsSuccess)
            {
                return Unavailable("read", readResult.Error);
            }

            using var readSecret = readResult.Value;
            if (!readSecret.ContentEquals(probeSecret))
            {
                return CredentialStoreAvailability.Unavailable(store.Name, $"{store.Name} returned a different value than the one stored during the availability self-test.");
            }

            var deleteResult = await store.Delete(key, cancellationToken);
            if (!deleteResult.IsSuccess)
            {
                return Unavailable("delete", deleteResult.Error);
            }

            deleted = true;
            return CredentialStoreAvailability.Available(store.Name);
        }
        finally
        {
            if (!deleted)
            {
                try
                {
                    await store.Delete(key, cancellationToken);
                }
                catch (Exception)
                {
                }
            }
        }
    }

    private CredentialStoreAvailability Unavailable(string operation, CredentialError error)
    {
        return CredentialStoreAvailability.Unavailable(store.Name, $"{store.Name} failed the availability self-test ({operation}): {error.Message}");
    }
}
