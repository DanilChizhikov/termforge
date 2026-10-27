using System.Diagnostics.CodeAnalysis;

namespace DTech.Core.Credentials;

public sealed class CredentialResult<T>
{
    private readonly T? _value;

    private CredentialResult(T? value, CredentialError? error)
    {
        _value = value;
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public CredentialError? Error { get; }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"The result has no value because it failed with {Error.Code}.");

    public static CredentialResult<T> Success(T value)
    {
        return new CredentialResult<T>(value, null);
    }

    public static CredentialResult<T> Failure(CredentialError error)
    {
        return new CredentialResult<T>(default, error);
    }
}
