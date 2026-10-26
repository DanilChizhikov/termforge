import { useCallback, useMemo } from 'react';
import { createRemoteSource } from '../api/files';
import { setRemotePath, useRemotePath } from '../state/filePaths';
import { useSelectedServerId } from '../state/selectedServer';
import { EmptyState } from './EmptyState';
import { FilePane } from './FilePane';

interface ServerFilePaneProps {
  serverId: string;
}

function ServerFilePane({ serverId }: ServerFilePaneProps) {
  const path = useRemotePath(serverId);
  const source = useMemo(() => createRemoteSource(serverId), [serverId]);
  const handlePathChange = useCallback((next: string) => setRemotePath(serverId, next), [serverId]);
  return (
    <FilePane
      title="Remote"
      sourceKey={`remote:${serverId}`}
      source={source}
      path={path}
      onPathChange={handlePathChange}
    />
  );
}

export function RemoteFilePane() {
  const serverId = useSelectedServerId();
  if (serverId === null) {
    return <EmptyState message="Select a server to browse remote files." />;
  }
  return <ServerFilePane key={serverId} serverId={serverId} />;
}
