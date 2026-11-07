using DTech.Core.Updates;

namespace DTech.Entrypoint.Updates;

public sealed record ReleasePackageRequest(ReleaseInfo Release, string RuntimeIdentifier, string DestinationDirectory);

public sealed record VerifiedReleasePackage(string Path, string Version, string RuntimeIdentifier);
