namespace DTech.Core.SettingsTransfer.Import;

public sealed record SettingsImportRequest(string SettingsFile, SecretsImportInput? Secrets, SettingsImportOptions Options)
{
    public override string ToString()
    {
        return $"{nameof(SettingsImportRequest)} {{ {nameof(SettingsFile)} = {SettingsFile.Length} characters, {nameof(Secrets)} = {Secrets}, {nameof(Options)} = {Options} }}";
    }
}
