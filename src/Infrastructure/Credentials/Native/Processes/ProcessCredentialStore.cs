using DTech.Core.Credentials;

namespace DTech.Credentials.Native.Processes;

public sealed class ProcessCredentialStore(IOperatingSystemCredentialStore rawStore, CredentialHelperRunner runner) : IOperatingSystemCredentialStore
{
    public string Name => rawStore.Name;

    public CredentialStoreAvailability CheckPrerequisites()
    {
        return CredentialStoreAvailability.Available(Name);
    }

    public async Task<CredentialResult> Store(CredentialKey key, Secret secret, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (secret.Length > CredentialHelperProtocol.MaximumSecretBytes)
        {
            var tooLarge = CredentialErrors.InvalidSecret("The operating system credential helper accepts secrets of at most 1 MiB.");
            return CredentialResult.Failure(tooLarge);
        }

        var metadata = CreateRequest(CredentialHelperOperation.Store, key);
        var bytes = secret.AsSpan().ToArray();
        using var request = new CredentialHelperPacket<CredentialHelperRequest>(metadata, bytes);
        using var response = await runner.RunAsync(request, cancellationToken);
        var error = response.Metadata.Error;
        return error is null ? CredentialResult.Success() : CredentialResult.Failure(error);
    }

    public async Task<CredentialResult<Secret>> Read(CredentialKey key, CancellationToken cancellationToken)
    {
        var metadata = CreateRequest(CredentialHelperOperation.Read, key);
        using var request = new CredentialHelperPacket<CredentialHelperRequest>(metadata, []);
        using var response = await runner.RunAsync(request, cancellationToken);
        var error = response.Metadata.Error;
        if (error is not null)
        {
            return CredentialResult<Secret>.Failure(error);
        }

        var secret = Secret.FromBytes(response.SecretBytes);
        return CredentialResult<Secret>.Success(secret);
    }

    public async Task<CredentialResult> Delete(CredentialKey key, CancellationToken cancellationToken)
    {
        var metadata = CreateRequest(CredentialHelperOperation.Delete, key);
        using var request = new CredentialHelperPacket<CredentialHelperRequest>(metadata, []);
        using var response = await runner.RunAsync(request, cancellationToken);
        var error = response.Metadata.Error;
        return error is null ? CredentialResult.Success() : CredentialResult.Failure(error);
    }

    private CredentialHelperRequest CreateRequest(CredentialHelperOperation operation, CredentialKey key)
    {
        return new CredentialHelperRequest(operation, key.Value, runner.OperationTimeoutMilliseconds);
    }
}
