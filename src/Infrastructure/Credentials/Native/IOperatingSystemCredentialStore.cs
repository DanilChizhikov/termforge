using DTech.Core.Credentials;

namespace DTech.Credentials.Native;

public interface IOperatingSystemCredentialStore : ICredentialStore
{
    string Name { get; }

    CredentialStoreAvailability CheckPrerequisites();
}
