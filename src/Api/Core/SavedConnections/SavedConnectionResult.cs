using System.Diagnostics.CodeAnalysis;

namespace DTech.Core.SavedConnections;

public sealed class SavedConnectionResult
{
    private static readonly SavedConnectionResult SuccessResult = new(null);

    private SavedConnectionResult(SavedConnectionError? error)
    {
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public SavedConnectionError? Error { get; }

    public static SavedConnectionResult Success()
    {
        return SuccessResult;
    }

    public static SavedConnectionResult Failure(SavedConnectionError error)
    {
        return new SavedConnectionResult(error);
    }
}

public sealed class SavedConnectionResult<T>
{
    private readonly T? _value;

    private SavedConnectionResult(T? value, SavedConnectionError? error)
    {
        _value = value;
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public SavedConnectionError? Error { get; }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"The result has no value because it failed with {Error.Code}.");

    public static SavedConnectionResult<T> Success(T value)
    {
        return new SavedConnectionResult<T>(value, null);
    }

    public static SavedConnectionResult<T> Failure(SavedConnectionError error)
    {
        return new SavedConnectionResult<T>(default, error);
    }
}
