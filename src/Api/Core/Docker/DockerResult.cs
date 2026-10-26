using System.Diagnostics.CodeAnalysis;

namespace DTech.Core.Docker;

public sealed class DockerResult<T>
{
    private readonly T? _value;

    private DockerResult(T? value, DockerError? error)
    {
        _value = value;
        Error = error;
    }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public DockerError? Error { get; }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"The result has no value because it failed with {Error.Code}.");

    public static DockerResult<T> Success(T value)
    {
        return new DockerResult<T>(value, null);
    }

    public static DockerResult<T> Failure(DockerError error)
    {
        return new DockerResult<T>(default, error);
    }
}
