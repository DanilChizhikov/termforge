using System.Collections.Concurrent;

namespace DTech.Ssh.IntegrationTests.Transfers;

internal sealed class ClientInterception
{
    public StreamInterception? Upload { get; set; }

    public bool FailUploadAfterSource { get; set; }

    public StreamInterception? Download { get; set; }

    public ConcurrentQueue<long> WriteOffsets { get; } = new();

    public ConcurrentQueue<long> ReadOffsets { get; } = new();

    public ConcurrentQueue<long> ProgressReports { get; } = new();
}
