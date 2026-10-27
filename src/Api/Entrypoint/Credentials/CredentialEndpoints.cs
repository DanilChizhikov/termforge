using DTech.Core.Credentials;
using DTech.Entrypoint.Credentials;

namespace Microsoft.AspNetCore.Builder;

public static class CredentialEndpoints
{
    public const string RoutePrefix = "/api/credentials";

    public static IEndpointRouteBuilder MapCredentialEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(RoutePrefix);
        group.MapGet("/status", GetStatus);
        group.MapPost("/vault/unlock", UnlockVault);
        group.MapPost("/vault/lock", LockVault);
        return endpoints;
    }

    private static async Task<IResult> GetStatus(ICredentialStoreStatusProvider statusProvider, CancellationToken cancellationToken)
    {
        var status = await statusProvider.GetStatus(cancellationToken);
        var availability = status.OperatingSystemStore;
        var operatingSystemStore = new OperatingSystemStoreStatusResponse(availability.StoreName, availability.IsAvailable, availability.Diagnostic);
        var backend = status.Backend.ToString();
        var vaultState = status.VaultState.ToString();
        var response = new CredentialStatusResponse(backend, status.SelectionReason, operatingSystemStore, vaultState);
        return Results.Ok(response);
    }

    private static async Task<IResult> UnlockVault(UnlockVaultRequest request, ICredentialVault vault, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.MasterPassword))
        {
            var missingError = CredentialErrors.InvalidSecret("the master password is required.");
            return CredentialErrorResults.ToProblem(missingError);
        }

        using var masterPassword = Secret.FromString(request.MasterPassword);
        request.MasterPassword = null;
        var result = await vault.Unlock(masterPassword, cancellationToken);
        if (!result.IsSuccess)
        {
            return CredentialErrorResults.ToProblem(result.Error);
        }

        return Results.NoContent();
    }

    private static IResult LockVault(ICredentialVault vault)
    {
        vault.Lock();
        return Results.NoContent();
    }
}
