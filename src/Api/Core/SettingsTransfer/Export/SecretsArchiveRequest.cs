using DTech.Core.Credentials;

namespace DTech.Core.SettingsTransfer.Export;

public sealed record SecretsArchiveRequest(Guid ExportId, IReadOnlyList<Guid> ConnectionIds, Secret Password)
{
    public override string ToString()
    {
        return $"{nameof(SecretsArchiveRequest)} {{ {nameof(ExportId)} = {ExportId}, {nameof(Password)} = {Secret.Redacted} }}";
    }
}
