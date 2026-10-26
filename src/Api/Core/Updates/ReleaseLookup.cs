namespace DTech.Core.Updates;

public sealed record ReleaseLookup(bool Succeeded, ReleaseInfo? Release)
{
    public static ReleaseLookup Failed { get; } = new(false, null);

    public static ReleaseLookup NoRelease { get; } = new(true, null);

    public static ReleaseLookup Found(ReleaseInfo release)
    {
        return new ReleaseLookup(true, release);
    }
}
