using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace DTech.Entrypoint.Updates;

internal sealed partial class UpdateOptionsValidator : IValidateOptions<UpdateOptions>
{
    public ValidateOptionsResult Validate(string? name, UpdateOptions options)
    {
        var failures = new List<string>();
        if (!RepositoryPattern().IsMatch(options.Repository))
        {
            failures.Add("Updates:Repository must look like owner/name");
        }

        var apiBaseUrlValid = Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out var apiBaseUrl);
        if (!apiBaseUrlValid || apiBaseUrl!.Scheme is not ("http" or "https"))
        {
            failures.Add("Updates:ApiBaseUrl must be an absolute http or https URL");
        }

        if (options.CacheMinutes <= 0)
        {
            failures.Add("Updates:CacheMinutes must be greater than zero");
        }

        if (options.FailureCacheMinutes <= 0)
        {
            failures.Add("Updates:FailureCacheMinutes must be greater than zero");
        }

        if (options.TimeoutSeconds <= 0)
        {
            failures.Add("Updates:TimeoutSeconds must be greater than zero");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    [GeneratedRegex(@"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")]
    private static partial Regex RepositoryPattern();
}
