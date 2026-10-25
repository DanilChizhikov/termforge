using System.Diagnostics.CodeAnalysis;

namespace DTech.Core.Credentials;

public sealed class CredentialResult
{
    private static readonly CredentialResult SuccessResult = new(null);

    private CredentialResult(CredentialError? error)
    {
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public CredentialError? Error { get; }

    public static CredentialResult Success()
    {
        return SuccessResult;
    }

    public static CredentialResult Failure(CredentialError error)
    {
        return new CredentialResult(error);
    }
}
