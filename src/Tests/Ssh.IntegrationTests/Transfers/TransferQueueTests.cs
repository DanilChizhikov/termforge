using DTech.Core.Operations;
using DTech.Core.Transfers;
using DTech.Core.Transfers.Execution;
using DTech.Core.Transfers.Queue;
using FluentAssertions;

namespace DTech.Ssh.IntegrationTests.Transfers;

[TestClass]
public sealed class TransferQueueTests
{
    private const int LargeFileSize = 3 * 1024 * 1024;
    private const int InterruptAfterBytes = 1024 * 1024;

    private static readonly TimeSpan SlowChunkDelay = TimeSpan.FromMilliseconds(40);

    [TestMethod]
    public async Task Upload_File_MatchesSourceHashAndReportsProgress()
    {
        await using var harness = await TransferHarness.OpenAsync();
        var source = harness.CreateLocalFile("payload.bin", LargeFileSize);
        var job = await harness.EnqueueAsync(TransferDirection.Upload, source, harness.RemoteDirectory);

        await harness.Runner.RunAsync(job);

        job.Status.Should().Be(OperationStatus.Succeeded);
        var remoteHash = await harness.RemoteHashAsync($"{harness.RemoteDirectory}/payload.bin");
        remoteHash.Should().Be(TransferHarness.LocalHash(source));
        var reports = harness.Interception.ProgressReports.ToArray();
        reports.Length.Should().BeGreaterThan(1);
        reports.Should().BeInAscendingOrder();
        reports[^1].Should().Be(LargeFileSize);
        job.Progress().TransferredBytes.Should().Be(LargeFileSize);
        harness.Journal.Get(job.Id).Status.Should().Be(OperationStatus.Succeeded);
        harness.Notifier.Changes.Should().Contain(change => change.Id == job.Id && change.Status == OperationStatus.Running);
        harness.Notifier.Changes.Last(change => change.Id == job.Id).Status.Should().Be(OperationStatus.Succeeded);
        var entries = await harness.ListRemoteAsync(harness.RemoteDirectory);
        entries.Should().ContainSingle(entry => entry.Name == "payload.bin");
    }

    [TestMethod]
    public async Task Upload_Directory_TransfersTreeRecursively()
    {
        await using var harness = await TransferHarness.OpenAsync();
        var first = harness.CreateLocalFile(Path.Combine("site", "index.html"), 4096);
        var nested = harness.CreateLocalFile(Path.Combine("site", "assets", "app.js"), 70000);
        var job = await harness.EnqueueAsync(TransferDirection.Upload, Path.Combine(harness.LocalDirectory, "site"), harness.RemoteDirectory);

        await harness.Runner.RunAsync(job);

        job.Status.Should().Be(OperationStatus.Succeeded);
        job.Progress().CompletedFiles.Should().Be(2);
        var firstHash = await harness.RemoteHashAsync($"{harness.RemoteDirectory}/site/index.html");
        var nestedHash = await harness.RemoteHashAsync($"{harness.RemoteDirectory}/site/assets/app.js");
        firstHash.Should().Be(TransferHarness.LocalHash(first));
        nestedHash.Should().Be(TransferHarness.LocalHash(nested));
    }

    [TestMethod]
    public async Task Download_Directory_TransfersTreeRecursivelyWithMatchingHashes()
    {
        await using var harness = await TransferHarness.OpenAsync();
        await harness.CreateRemoteDirectoryAsync("logs");
        await harness.CreateRemoteDirectoryAsync("logs/archive");
        var current = await harness.CreateRemoteFileAsync("logs/app.log", 200000);
        var archived = await harness.CreateRemoteFileAsync("logs/archive/app.1.log", 5000);
        var job = await harness.EnqueueAsync(TransferDirection.Download, $"{harness.RemoteDirectory}/logs", harness.LocalDirectory);

        await harness.Runner.RunAsync(job);

        job.Status.Should().Be(OperationStatus.Succeeded);
        var currentHash = await harness.RemoteHashAsync(current);
        var archivedHash = await harness.RemoteHashAsync(archived);
        TransferHarness.LocalHash(Path.Combine(harness.LocalDirectory, "logs", "app.log")).Should().Be(currentHash);
        TransferHarness.LocalHash(Path.Combine(harness.LocalDirectory, "logs", "archive", "app.1.log")).Should().Be(archivedHash);
        Directory.GetFiles(harness.LocalDirectory, "*" + TransferPartialName.Suffix, SearchOption.AllDirectories).Should().BeEmpty();
    }

    [TestMethod]
    public async Task Upload_Cancelled_LeavesNoPartialOrTarget()
    {
        await using var harness = await TransferHarness.OpenAsync();
        harness.Interception.Upload = new StreamInterception { Delay = SlowChunkDelay };
        var source = harness.CreateLocalFile("slow.bin", LargeFileSize);
        var job = await harness.EnqueueAsync(TransferDirection.Upload, source, harness.RemoteDirectory);

        var running = harness.Runner.RunAsync(job);
        await TransferHarness.WaitUntilAsync(() => job.TransferredBytes > 0);
        var outcome = await harness.Control.CancelAsync(job.Id);
        await running;

        outcome.Should().Be(TransferCancelOutcome.CancellationRequested);
        job.Status.Should().Be(OperationStatus.Cancelled);
        harness.Journal.Get(job.Id).Status.Should().Be(OperationStatus.Cancelled);
        var entries = await harness.ListRemoteAsync(harness.RemoteDirectory);
        entries.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Download_Cancelled_LeavesNoPartialOrTarget()
    {
        await using var harness = await TransferHarness.OpenAsync();
        harness.Interception.Download = new StreamInterception { Delay = SlowChunkDelay };
        var source = await harness.CreateRemoteFileAsync("slow.bin", LargeFileSize);
        var job = await harness.EnqueueAsync(TransferDirection.Download, source, harness.LocalDirectory);

        var running = harness.Runner.RunAsync(job);
        await TransferHarness.WaitUntilAsync(() => job.TransferredBytes > 0);
        await harness.Control.CancelAsync(job.Id);
        await running;

        job.Status.Should().Be(OperationStatus.Cancelled);
        Directory.EnumerateFileSystemEntries(harness.LocalDirectory).Should().BeEmpty();
    }

    [TestMethod]
    public async Task Upload_InterruptedThenRetried_ResumesFromPartialWithIntegrity()
    {
        await using var harness = await TransferHarness.OpenAsync();
        harness.Interception.Upload = new StreamInterception { ReadLimit = InterruptAfterBytes };
        harness.Interception.FailUploadAfterSource = true;
        var source = harness.CreateLocalFile("resume.bin", LargeFileSize);
        var job = await harness.EnqueueAsync(TransferDirection.Upload, source, harness.RemoteDirectory);

        await harness.Runner.RunAsync(job);

        job.Status.Should().Be(OperationStatus.Failed);
        var failed = job.Snapshot();
        failed.Retryable.Should().BeTrue();
        failed.PartialBytes.Should().Be(InterruptAfterBytes);
        var partials = await harness.ListRemoteAsync(harness.RemoteDirectory);
        partials.Should().ContainSingle(entry => entry.Name.EndsWith(TransferPartialName.Suffix) && entry.Size == InterruptAfterBytes);

        harness.Interception = new ClientInterception();
        var retried = await harness.Enqueuer.RetryAsync(harness.Journal.Get(job.Id), CancellationToken.None);
        retried.IsSuccess.Should().BeTrue(retried.Error?.Message);
        var resumed = harness.Queue.Find(retried.Transfers[0].Id)!;
        await harness.Runner.RunAsync(resumed);

        resumed.Status.Should().Be(OperationStatus.Succeeded);
        harness.Interception.WriteOffsets.Should().Equal(InterruptAfterBytes);
        var remoteHash = await harness.RemoteHashAsync($"{harness.RemoteDirectory}/resume.bin");
        remoteHash.Should().Be(TransferHarness.LocalHash(source));
        var entries = await harness.ListRemoteAsync(harness.RemoteDirectory);
        entries.Should().ContainSingle().Which.Name.Should().Be("resume.bin");
    }

    [TestMethod]
    public async Task Download_InterruptedThenRetried_ResumesFromPartialWithIntegrity()
    {
        await using var harness = await TransferHarness.OpenAsync();
        harness.Interception.Download = new StreamInterception { WriteLimit = InterruptAfterBytes };
        var source = await harness.CreateRemoteFileAsync("resume.bin", LargeFileSize);
        var job = await harness.EnqueueAsync(TransferDirection.Download, source, harness.LocalDirectory);

        await harness.Runner.RunAsync(job);

        job.Status.Should().Be(OperationStatus.Failed);
        job.Snapshot().Retryable.Should().BeTrue();
        var partial = Directory.GetFiles(harness.LocalDirectory, "*" + TransferPartialName.Suffix).Should().ContainSingle().Subject;
        new FileInfo(partial).Length.Should().Be(InterruptAfterBytes);

        harness.Interception = new ClientInterception();
        var retried = await harness.Enqueuer.RetryAsync(harness.Journal.Get(job.Id), CancellationToken.None);
        retried.IsSuccess.Should().BeTrue(retried.Error?.Message);
        var resumed = harness.Queue.Find(retried.Transfers[0].Id)!;
        await harness.Runner.RunAsync(resumed);

        resumed.Status.Should().Be(OperationStatus.Succeeded);
        harness.Interception.ReadOffsets.Should().Equal(InterruptAfterBytes);
        var remoteHash = await harness.RemoteHashAsync(source);
        TransferHarness.LocalHash(Path.Combine(harness.LocalDirectory, "resume.bin")).Should().Be(remoteHash);
        File.Exists(partial).Should().BeFalse();
    }

    [TestMethod]
    public async Task Upload_IntoReadOnlyDirectory_FailsWithoutRetryAndLeavesNothing()
    {
        await using var harness = await TransferHarness.OpenAsync();
        var source = harness.CreateLocalFile($"denied-{Guid.NewGuid():N}.txt", 128);
        var job = await harness.EnqueueAsync(TransferDirection.Upload, source, "/etc");

        await harness.Runner.RunAsync(job);

        job.Status.Should().Be(OperationStatus.Failed);
        var snapshot = job.Snapshot();
        snapshot.Retryable.Should().BeFalse();
        snapshot.Error.Should().Contain("Permission denied");
        harness.Journal.Get(job.Id).Retryable.Should().BeFalse();
    }

    [TestMethod]
    public async Task ParallelUploads_RunConcurrentlyAndBothKeepIntegrity()
    {
        await using var harness = await TransferHarness.OpenAsync();
        harness.Interception.Upload = new StreamInterception { Delay = TimeSpan.FromMilliseconds(5) };
        var first = harness.CreateLocalFile("first.bin", LargeFileSize);
        var second = harness.CreateLocalFile("second.bin", LargeFileSize);
        var firstJob = await harness.EnqueueAsync(TransferDirection.Upload, first, harness.RemoteDirectory);
        var secondJob = await harness.EnqueueAsync(TransferDirection.Upload, second, harness.RemoteDirectory);
        var overlapped = false;

        var firstRun = harness.Runner.RunAsync(firstJob);
        var secondRun = harness.Runner.RunAsync(secondJob);
        var watcher = TransferHarness.WaitUntilAsync(() =>
        {
            overlapped |= firstJob.TransferredBytes > 0 && secondJob.TransferredBytes > 0
                && firstJob.Status == OperationStatus.Running && secondJob.Status == OperationStatus.Running;
            return overlapped || (firstRun.IsCompleted && secondRun.IsCompleted);
        });
        await Task.WhenAll(firstRun, secondRun, watcher);

        overlapped.Should().BeTrue();
        firstJob.Status.Should().Be(OperationStatus.Succeeded);
        secondJob.Status.Should().Be(OperationStatus.Succeeded);
        var firstHash = await harness.RemoteHashAsync($"{harness.RemoteDirectory}/first.bin");
        var secondHash = await harness.RemoteHashAsync($"{harness.RemoteDirectory}/second.bin");
        firstHash.Should().Be(TransferHarness.LocalHash(first));
        secondHash.Should().Be(TransferHarness.LocalHash(second));
    }

    [TestMethod]
    public async Task Shutdown_DuringTransfer_CancelsRunningAndQueuedAndCleansUp()
    {
        await using var harness = await TransferHarness.OpenAsync();
        harness.Interception.Upload = new StreamInterception { Delay = SlowChunkDelay };
        var running = harness.CreateLocalFile("running.bin", LargeFileSize);
        var waiting = harness.CreateLocalFile("waiting.bin", 1024);
        var runningJob = await harness.EnqueueAsync(TransferDirection.Upload, running, harness.RemoteDirectory);
        var waitingJob = await harness.EnqueueAsync(TransferDirection.Upload, waiting, harness.RemoteDirectory);

        var run = harness.Runner.RunAsync(runningJob);
        await TransferHarness.WaitUntilAsync(() => runningJob.TransferredBytes > 0);
        await harness.Control.CancelAllAsync();
        await run;
        await harness.Runner.RunAsync(waitingJob);

        runningJob.Status.Should().Be(OperationStatus.Cancelled);
        waitingJob.Status.Should().Be(OperationStatus.Cancelled);
        harness.Journal.Get(runningJob.Id).Status.Should().Be(OperationStatus.Cancelled);
        harness.Journal.Get(waitingJob.Id).Status.Should().Be(OperationStatus.Cancelled);
        var entries = await harness.ListRemoteAsync(harness.RemoteDirectory);
        entries.Should().BeEmpty();
        var late = await harness.EnqueueBatchAsync(TransferDirection.Upload, waiting, harness.RemoteDirectory, false);
        late.IsSuccess.Should().BeFalse();
    }

    [TestMethod]
    public async Task Enqueue_ExistingTarget_RequiresOverwriteDecision()
    {
        await using var harness = await TransferHarness.OpenAsync();
        await harness.CreateRemoteFileAsync("config.json", 64);
        var replacement = harness.CreateLocalFile("config.json", 512);

        var refused = await harness.EnqueueBatchAsync(TransferDirection.Upload, replacement, harness.RemoteDirectory, false);
        var confirmed = await harness.EnqueueBatchAsync(TransferDirection.Upload, replacement, harness.RemoteDirectory, true);
        var job = harness.Queue.Find(confirmed.Transfers[0].Id)!;
        await harness.Runner.RunAsync(job);

        refused.Conflicts.Should().Equal($"{harness.RemoteDirectory}/config.json");
        confirmed.IsSuccess.Should().BeTrue();
        job.Status.Should().Be(OperationStatus.Succeeded);
        var remoteHash = await harness.RemoteHashAsync($"{harness.RemoteDirectory}/config.json");
        remoteHash.Should().Be(TransferHarness.LocalHash(replacement));
    }
}
