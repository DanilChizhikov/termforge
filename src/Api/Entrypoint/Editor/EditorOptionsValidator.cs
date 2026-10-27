using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Editor;

internal sealed class EditorOptionsValidator : IValidateOptions<EditorOptions>
{
    private const long MaxSupportedFileBytes = 64 * 1024 * 1024;

    public ValidateOptionsResult Validate(string? name, EditorOptions options)
    {
        var failures = new List<string>();
        if (options.MaxFileBytes <= 0 || options.MaxFileBytes > MaxSupportedFileBytes)
        {
            failures.Add($"Editor:MaxFileBytes must be between 1 and {MaxSupportedFileBytes}");
        }

        if (options.MaxBackupsPerFile <= 0)
        {
            failures.Add("Editor:MaxBackupsPerFile must be greater than zero");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
