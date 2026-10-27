namespace DTech.Core.Updates;

public interface ICurrentVersionProvider
{
    SemanticVersion Current { get; }
}
