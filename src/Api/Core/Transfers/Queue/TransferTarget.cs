namespace DTech.Core.Transfers.Queue;

public readonly record struct TransferTarget(TransferRequest Request, string Path)
{
    public bool Matches(TransferTarget other)
    {
        var local = Request.Direction == TransferDirection.Download;
        var otherLocal = other.Request.Direction == TransferDirection.Download;
        if (local != otherLocal)
        {
            return false;
        }

        if (!local && Request.DestinationConnectionId != other.Request.DestinationConnectionId)
        {
            return false;
        }

        var comparison = local && !OperatingSystem.IsLinux()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(Path, other.Path, comparison);
    }
}
