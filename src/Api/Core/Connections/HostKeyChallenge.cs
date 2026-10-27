namespace DTech.Core.Connections;

public sealed record HostKeyChallenge(HostKeyVerificationRequest Request, HostKeyVerification Verification);
