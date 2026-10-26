using DTech.Core.Files;

namespace DTech.Core.Transfers.Execution;

public sealed class RemoteTransferPlanner
{
    public async Task<FileResult<TransferPlan>> PlanAsync(RemotePlanContext context, TransferRoot root, CancellationToken cancellationToken)
    {
        var stat = await context.Client.StatAsync(root.SourcePath, cancellationToken);
        if (!stat.IsSuccess)
        {
            var statError = FileErrorMapper.FromSsh(stat.Error!, root.SourcePath);
            return FileResult<TransferPlan>.Failure(statError);
        }

        var entry = stat.Value;
        var kind = ResolveKind(entry);
        if (kind == RemoteFileKind.File)
        {
            var item = CreateFileItem(entry, root.TargetPath, root.TargetName);
            var single = new TransferPlan([item]);
            return FileResult<TransferPlan>.Success(single);
        }

        if (kind != RemoteFileKind.Directory)
        {
            var unsupported = FileError.Invalid($"Only files and directories can be transferred: {root.SourcePath}");
            return FileResult<TransferPlan>.Failure(unsupported);
        }

        return await WalkAsync(context, root, cancellationToken);
    }

    public static RemoteFileKind ResolveKind(RemoteFileEntry entry)
    {
        if (entry.Kind != RemoteFileKind.SymbolicLink)
        {
            return entry.Kind;
        }

        return entry.LinkTargetKind ?? RemoteFileKind.Other;
    }

    private static async Task<FileResult<TransferPlan>> WalkAsync(RemotePlanContext walk, TransferRoot root, CancellationToken cancellationToken)
    {
        var items = new List<TransferItem>();
        var pending = new Stack<TransferRoot>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            var directoryItem = new TransferItem(current.SourcePath, current.TargetPath, true) { TargetName = current.TargetName };
            items.Add(directoryItem);
            var listed = await walk.Client.ListAsync(current.SourcePath, cancellationToken);
            if (!listed.IsSuccess)
            {
                var listError = FileErrorMapper.FromSsh(listed.Error!, current.SourcePath);
                return FileResult<TransferPlan>.Failure(listError);
            }

            var children = listed.Value;
            for (var i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child.Kind is not (RemoteFileKind.File or RemoteFileKind.Directory))
                {
                    continue;
                }

                var nameError = LocalNameGuard.Validate(child.Name);
                if (nameError is not null)
                {
                    var unsafeName = FileError.Invalid($"Remote entry name cannot be stored locally: {child.FullPath}");
                    return FileResult<TransferPlan>.Failure(unsafeName);
                }

                var childTarget = walk.CombineTarget(current.TargetPath, child.Name);
                if (child.Kind == RemoteFileKind.Directory)
                {
                    var nested = new TransferRoot(child.FullPath, childTarget, child.Name);
                    pending.Push(nested);
                    continue;
                }

                var fileItem = CreateFileItem(child, childTarget, child.Name);
                items.Add(fileItem);
            }
        }

        var plan = new TransferPlan(items);
        return FileResult<TransferPlan>.Success(plan);
    }

    private static TransferItem CreateFileItem(RemoteFileEntry entry, string targetPath, string targetName)
    {
        return new TransferItem(entry.FullPath, targetPath, false)
        {
            Size = entry.Size,
            ModifiedUtc = entry.LastWriteTimeUtc,
            TargetName = targetName,
        };
    }
}
