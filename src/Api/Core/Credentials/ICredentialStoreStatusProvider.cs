namespace DTech.Core.Credentials;

public interface ICredentialStoreStatusProvider
{
    Task<CredentialStoreStatus> GetStatus(CancellationToken cancellationToken);
}
