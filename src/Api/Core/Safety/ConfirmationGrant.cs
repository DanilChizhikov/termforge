namespace DTech.Core.Safety;

public sealed record ConfirmationGrant(string Token, DateTimeOffset ExpiresAt);
