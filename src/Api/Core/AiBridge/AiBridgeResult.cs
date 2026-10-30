using System.Diagnostics.CodeAnalysis;

namespace DTech.Core.AiBridge;

public sealed class AiBridgeResult<T>
{
    private readonly T? _value;

    private AiBridgeResult(T? value, AiBridgeError? error)
    {
        _value = value;
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public AiBridgeError? Error { get; }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"The result has no value because it failed with {Error.Code}.");

    public static AiBridgeResult<T> Success(T value)
    {
        return new AiBridgeResult<T>(value, null);
    }

    public static AiBridgeResult<T> Failure(AiBridgeError error)
    {
        return new AiBridgeResult<T>(default, error);
    }
}
