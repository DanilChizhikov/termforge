using System.Runtime.InteropServices;
using DTech.Core.Credentials;

namespace DTech.Credentials.Native;

public sealed class UnsupportedPlatformCredentialStore : IOperatingSystemCredentialStore
{
    public string Name => "Operating system credential store";

    private static string Diagnostic => $"No supported operating system credential store exists for {RuntimeInformation.OSDescription}. Windows, macOS and Linux are supported.";

    public CredentialStoreAvailability CheckPrerequisites()
    {
        return CredentialStoreAvailability.Unavailable(Name, Diagnostic);
    }

    public Task<CredentialResult> Store(CredentialKey key, Secret secret, CancellationToken cancellationToken)
    {
        var error = CredentialErrors.StoreUnavailable(Diagnostic);
        var result = CredentialResult.Failure(error);
        return Task.FromResult(result);
    }

    public Task<CredentialResult<Secret>> Read(CredentialKey key, CancellationToken cancellationToken)
    {
        var error = CredentialErrors.StoreUnavailable(Diagnostic);
        var result = CredentialResult<Secret>.Failure(error);
        return Task.FromResult(result);
    }

    public Task<CredentialResult> Delete(CredentialKey key, CancellationToken cancellationToken)
    {
        var error = CredentialErrors.StoreUnavailable(Diagnostic);
        var result = CredentialResult.Failure(error);
        return Task.FromResult(result);
    }
}
