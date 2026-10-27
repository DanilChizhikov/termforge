namespace DTech.Core.Operations;

public interface IOperationSanitizer
{
    IReadOnlyDictionary<string, string> SanitizeDetails(IReadOnlyDictionary<string, string>? details);

    string? SanitizeText(string? text);
}
