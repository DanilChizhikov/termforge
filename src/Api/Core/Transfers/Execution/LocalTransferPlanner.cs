using DTech.Core.Files;
using DTech.Core.Files.Local;

namespace DTech.Core.Transfers.Execution;

public sealed class LocalTransferPlanner
{
    public FileResult<TransferPlan> Plan(TransferRoot root, Func<string, string, string> combineTarget, CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(root.SourcePath))
            {
                var file = new FileInfo(root.SourcePath);
                var item = CreateFileItem(file, root.TargetPath, root.TargetName);
                var single = new TransferPlan([item]);
                return FileResult<TransferPlan>.Success(single);
            }

            if (!Directory.Exists(root.SourcePath))
            {
                var missing = FileError.NotFound(root.SourcePath);
                return FileResult<TransferPlan>.Failure(missing);
            }

            var directory = new DirectoryInfo(root.SourcePath);
            var walk = new DirectoryWalk(directory, root.TargetPath, root.TargetName);
            var items = Walk(walk, combineTarget, cancellationToken);
            var plan = new TransferPlan(items);
            return FileResult<TransferPlan>.Success(plan);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            var error = LocalIoErrors.Map(exception, root.SourcePath);
            return FileResult<TransferPlan>.Failure(error);
        }
    }

    private static List<TransferItem> Walk(DirectoryWalk root, Func<string, string, string> combineTarget, CancellationToken cancellationToken)
    {
        var items = new List<TransferItem>();
        var pending = new Stack<DirectoryWalk>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            var directoryItem = new TransferItem(current.Directory.FullName, current.TargetPath, true) { TargetName = current.TargetName };
            items.Add(directoryItem);
            foreach (var child in current.Directory.EnumerateFileSystemInfos())
            {
                if (child.LinkTarget is not null)
                {
                    continue;
                }

                var childTarget = combineTarget(current.TargetPath, child.Name);
                if (child is DirectoryInfo childDirectory)
                {
                    var nested = new DirectoryWalk(childDirectory, childTarget, child.Name);
                    pending.Push(nested);
                    continue;
                }

                if (child is FileInfo childFile)
                {
                    var fileItem = CreateFileItem(childFile, childTarget, child.Name);
                    items.Add(fileItem);
                }
            }
        }

        return items;
    }

    private static TransferItem CreateFileItem(FileInfo file, string targetPath, string targetName)
    {
        return new TransferItem(file.FullName, targetPath, false)
        {
            Size = file.Length,
            ModifiedUtc = file.LastWriteTimeUtc,
            TargetName = targetName,
        };
    }

    private sealed record DirectoryWalk(DirectoryInfo Directory, string TargetPath, string TargetName);
}
