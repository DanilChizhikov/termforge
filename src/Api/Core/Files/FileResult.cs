using System.Diagnostics.CodeAnalysis;

namespace DTech.Core.Files;

public sealed class FileResult
{
    private static readonly FileResult SuccessResult = new(null);

    private FileResult(FileError? error)
    {
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public FileError? Error { get; }

    public static FileResult Success()
    {
        return SuccessResult;
    }

    public static FileResult Failure(FileError error)
    {
        return new FileResult(error);
    }
}

public sealed class FileResult<T>
{
    private readonly T? _value;

    private FileResult(T? value, FileError? error)
    {
        _value = value;
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public FileError? Error { get; }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"The result has no value because it failed with {Error.Code}.");

    public static FileResult<T> Success(T value)
    {
        return new FileResult<T>(value, null);
    }

    public static FileResult<T> Failure(FileError error)
    {
        return new FileResult<T>(default, error);
    }
}
