using DTech.Core.Credentials;

namespace DTech.Core.SettingsTransfer.Secrets;

public sealed record ArchivedSecret(Guid ConnectionId, ArchivedSecretKind Kind, Secret Value);
