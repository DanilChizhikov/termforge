namespace DTech.Entrypoint.Security.Network;

internal sealed record PasswordLoginStatusResponse(bool PasswordLoginEnabled, bool SignedInWithPassword);
