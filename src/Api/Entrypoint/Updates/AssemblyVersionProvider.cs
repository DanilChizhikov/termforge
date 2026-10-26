using System.Reflection;
using DTech.Core.Updates;

namespace DTech.Entrypoint.Updates;

internal sealed class AssemblyVersionProvider : ICurrentVersionProvider
{
    public AssemblyVersionProvider()
    {
        var assembly = typeof(AssemblyVersionProvider).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var parsed = SemanticVersion.TryParse(informational, out var version);
        if (!parsed)
        {
            var assemblyVersion = assembly.GetName().Version?.ToString(3);
            parsed = SemanticVersion.TryParse(assemblyVersion, out version);
        }

        SemanticVersion.TryParse("0.0.0", out var fallback);
        Current = parsed ? version! : fallback!;
    }

    public SemanticVersion Current { get; }
}
