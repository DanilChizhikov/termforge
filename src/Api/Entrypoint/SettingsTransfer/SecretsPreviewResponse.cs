namespace DTech.Entrypoint.SettingsTransfer;

public sealed record SecretsPreviewResponse(bool Supplied, int Passwords, int SudoPasswords);
