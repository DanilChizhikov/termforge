import { useCallback, useMemo } from 'react';
import { createRemoteSource, type FileEntry } from '../api/files';
import { createRemoteSearch } from '../api/fileSearch';
import { openEditorFile } from '../state/editorFiles';
import { setRemotePath, useRemotePath } from '../state/filePaths';
import { useSelectedServerId } from '../state/selectedServer';
import { RegionToggle } from './CollapsibleRegion';
import { EmptyState } from './EmptyState';
import { FilePane } from './FilePane';
import { useFinishedTransferCount, useRemotePaneTransfer } from './useTransferTargets';

interface ServerFilePaneProps {
  serverId: string;
}

function ServerFilePane({ serverId }: ServerFilePaneProps) {
  const path = useRemotePath(serverId);
  const source = useMemo(() => createRemoteSource(serverId), [serverId]);
  const search = useMemo(() => createRemoteSearch(serverId), [serverId]);
  const handlePathChange = useCallback((next: string) => setRemotePath(serverId, next), [serverId]);
  const handleOpenFile = useCallback((entry: FileEntry) => openEditorFile(serverId, entry.path), [serverId]);
  const transfer = useRemotePaneTransfer(serverId);
  const finishedUploads = useFinishedTransferCount('upload', serverId);
  return (
    <FilePane
      title="Remote"
      sourceKey={`remote:${serverId}`}
      source={source}
      path={path}
      onPathChange={handlePathChange}
      onOpenFile={handleOpenFile}
      transfer={transfer}
      refreshToken={finishedUploads}
      search={search}
    />
  );
}

export function RemoteFilePane() {
  const serverId = useSelectedServerId();
  if (serverId === null) {
    return (
      <div className="file-pane">
        <div className="file-toolbar">
          <span className="section-label file-pane-label">Remote</span>
          <span className="file-toolbar-spacer" />
          <RegionToggle />
        </div>
        <EmptyState message="Select a server to browse remote files." />
      </div>
    );
  }
  return <ServerFilePane key={serverId} serverId={serverId} />;
}
