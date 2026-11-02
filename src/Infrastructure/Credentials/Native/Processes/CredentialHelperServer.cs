using DTech.Core.Credentials;

namespace DTech.Credentials.Native.Processes;

public sealed class CredentialHelperServer(Func<TimeSpan, IOperatingSystemCredentialStore> stores)
{
    public async Task RunAsync(Stream input, Stream output, CancellationToken cancellationToken)
    {
        using var request = await CredentialHelperProtocol.ReadAsync<CredentialHelperRequest>(input, cancellationToken);
        using var response = await ExecuteAsync(request, cancellationToken);
        await CredentialHelperProtocol.WriteAsync(output, response, cancellationToken);
    }

    private async Task<CredentialHelperPacket<CredentialHelperResponse>> ExecuteAsync(CredentialHelperPacket<CredentialHelperRequest> request, CancellationToken cancellationToken)
    {
        var metadata = request.Metadata;
        var key = CredentialKey.Create(metadata.Key);
        if (!key.IsSuccess)
        {
            return Failure(key.Error);
        }

        if (!Enum.IsDefined(metadata.Operation) || metadata.TimeoutMilliseconds <= 0 ||
            metadata.TimeoutMilliseconds > CredentialStoreOptions.MaximumNativeTimeout.TotalMilliseconds)
        {
            var invalid = CredentialErrors.StoreFailure("The credential helper request is invalid.");
            return Failure(invalid);
        }

        var timeout = TimeSpan.FromMilliseconds(metadata.TimeoutMilliseconds);
        using var deadline = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            var store = stores(timeout);
            var prerequisites = store.CheckPrerequisites();
            if (!prerequisites.IsAvailable)
            {
                var unavailable = CredentialErrors.StoreUnavailable(prerequisites.Diagnostic);
                return Failure(unavailable);
            }

            switch (metadata.Operation)
            {
                case CredentialHelperOperation.Store:
                    using (var secret = Secret.FromBytes(request.SecretBytes))
                    {
                        var result = await store.Store(key.Value, secret, linked.Token);
                        return result.IsSuccess ? Success([]) : Failure(result.Error);
                    }
                case CredentialHelperOperation.Read:
                    var read = await store.Read(key.Value, linked.Token);
                    if (!read.IsSuccess)
                    {
                        return Failure(read.Error);
                    }

                    using (var secret = read.Value)
                    {
                        var bytes = secret.AsSpan().ToArray();
                        return Success(bytes);
                    }
                default:
                    var deleted = await store.Delete(key.Value, linked.Token);
                    return deleted.IsSuccess ? Success([]) : Failure(deleted.Error);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var timeoutError = CredentialErrors.StoreUnavailable("The operating system credential operation exceeded its deadline; its result is unknown.");
            return Failure(timeoutError);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var failed = CredentialErrors.StoreFailure($"The operating system credential operation failed ({exception.GetType().Name}); its result is unknown.");
            return Failure(failed);
        }
    }

    private static CredentialHelperPacket<CredentialHelperResponse> Success(byte[] bytes)
    {
        var metadata = new CredentialHelperResponse();
        return new CredentialHelperPacket<CredentialHelperResponse>(metadata, bytes);
    }

    private static CredentialHelperPacket<CredentialHelperResponse> Failure(CredentialError error)
    {
        var metadata = new CredentialHelperResponse(error);
        return new CredentialHelperPacket<CredentialHelperResponse>(metadata, []);
    }
}
