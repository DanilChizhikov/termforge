using DTech.Core.Editor;
using DTech.Core.Files.Remote;

namespace DTech.Core.Tests.Editor;

internal sealed class EditorGraph
{
    public const long DefaultMaxFileBytes = 1024;
    public const int DefaultMaxBackups = 3;

    public EditorGraph(EditorLimits limits, TimeProvider timeProvider)
    {
        var names = new AuxiliaryFileNames();
        Codec = new TextContentCodec();
        var normalizer = new RemotePathNormalizer();
        Paths = new EditorPathResolver(normalizer);
        Snapshots = new RemoteFileSnapshotReader(limits);
        Documents = new RemoteDocumentReader(Snapshots, Codec);
        Catalog = new BackupCatalog(names);
        var conflicts = new ConflictDetector(Codec);
        var backups = new BackupWriter(names, timeProvider);
        var replacer = new FileReplacer(names);
        var pruner = new BackupPruner(Catalog, limits);
        Saver = new RemoteFileSaver(Snapshots, conflicts, backups, replacer, pruner, limits);
        Rollback = new RemoteFileRollback(Catalog, Snapshots, Saver);
    }

    public TextContentCodec Codec { get; }

    public EditorPathResolver Paths { get; }

    public RemoteFileSnapshotReader Snapshots { get; }

    public RemoteDocumentReader Documents { get; }

    public BackupCatalog Catalog { get; }

    public RemoteFileSaver Saver { get; }

    public RemoteFileRollback Rollback { get; }

    public static EditorGraph CreateDefault(TimeProvider timeProvider)
    {
        var limits = new EditorLimits(DefaultMaxFileBytes, DefaultMaxBackups);
        return new EditorGraph(limits, timeProvider);
    }
}
