namespace DTech.Core.Transfers.Execution;

public sealed record TransferPlan(IReadOnlyList<TransferItem> Items)
{
    public long TotalBytes
    {
        get
        {
            long total = 0;
            for (var i = 0; i < Items.Count; i++)
            {
                total += Items[i].Size;
            }

            return total;
        }
    }

    public int FileCount
    {
        get
        {
            var count = 0;
            for (var i = 0; i < Items.Count; i++)
            {
                if (!Items[i].IsDirectory)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
