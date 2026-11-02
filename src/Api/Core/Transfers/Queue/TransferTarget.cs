namespace DTech.Core.Transfers.Queue;

public readonly record struct TransferTarget(TransferRequest Request, string Path)
{
    public bool Matches(TransferTarget other)
    {
        if (Request.Direction != other.Request.Direction)
        {
            return false;
        }

        if (Request.Direction == TransferDirection.Upload && Request.ConnectionId != other.Request.ConnectionId)
        {
            return false;
        }

        var comparison = Request.Direction == TransferDirection.Download && !OperatingSystem.IsLinux()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(Path, other.Path, comparison);
    }
}
