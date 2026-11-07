import { useCallback, useEffect, useMemo, useState } from 'react';
import { Group, Separator } from 'react-resizable-panels';
import { createLocalSource, createRemoteSource, getLocalRoots, type LocalRoot } from '../api/files';
import { createLocalSearch, createRemoteSearch } from '../api/fileSearch';
import type { TransferStartRequest } from '../api/transfers';
import { useConnectionsSnapshot } from '../state/connections';
import { getFilePaths, setLocalPath, setRemotePath } from '../state/filePaths';
import { getSelectedServerId } from '../state/selectedServer';
import { requestTransfers, useTransfersState } from '../state/transfers';
import { openEditorFile } from '../state/editorFiles';
import { usePersistentLayout } from '../state/workspaceLayout';
import { CollapsibleRegion, RegionToggle } from './CollapsibleRegion';
import { EmptyState } from './EmptyState';
import { FilePane } from './FilePane';
import type { FilePaneTransfer } from './filePaneTransfer';
import { ArrowLeftIcon, ArrowRightIcon } from './icons/Icons';

export interface BrowserLocation {
  source: string | null;
  path: string | null;
}

export function browserTransfer(source: BrowserLocation, target: BrowserLocation, paths: string[]): TransferStartRequest | null {
  if (source.source === null || target.source === null || target.path === null || paths.length === 0
    || (source.source === 'local' && target.source === 'local')) return null;
  return {
    connectionId: source.source === 'local' ? target.source : source.source,
    ...(source.source !== 'local' && target.source !== 'local' ? { targetConnectionId: target.source } : {}),
    direction: source.source === 'local' ? 'upload' : target.source === 'local' ? 'download' : 'remoteToRemote',
    sourcePaths: paths,
    targetDirectory: target.path,
    overwrite: false,
  };
}

function initialLocation(source: string | null): BrowserLocation {
  const paths = getFilePaths();
  return { source, path: source === 'local' ? paths.localPath : paths.remotePaths.get(source ?? '') ?? null };
}

export function DualFileBrowser() {
  const layout = usePersistentLayout('file-panes');
  const [left, setLeft] = useState(() => initialLocation('local'));
  const [right, setRight] = useState(() => initialLocation(getSelectedServerId()));
  return (
    <Group orientation="horizontal" id="file-panes" className="group" {...layout}>
      <CollapsibleRegion id="local-files" title={left.source === 'local' ? 'Local' : 'Left files'} defaultSize="50%" minSize="15%" collapsedLayout="rail">
        <BrowserPane id="left" location={left} target={right} onChange={setLeft} />
      </CollapsibleRegion>
      <Separator className="separator separator-vertical" aria-label="Resize panels" />
      <CollapsibleRegion id="remote-files" title={right.source === 'local' ? 'Right files' : 'Remote'} defaultSize="50%" minSize="15%" collapsedLayout="rail">
        <BrowserPane id="right" location={right} target={left} onChange={setRight} />
      </CollapsibleRegion>
    </Group>
  );
}

interface BrowserPaneProps {
  id: 'left' | 'right';
  location: BrowserLocation;
  target: BrowserLocation;
  onChange: (location: BrowserLocation) => void;
}

function BrowserPane({ id, location, target, onChange }: BrowserPaneProps) {
  const connections = useConnectionsSnapshot();
  const title = id === 'left' ? 'Left' : 'Right';
  const unavailable = location.source === null || (location.source !== 'local' && !connections.some((connection) => connection.id === location.source));
  return (
    <div className="browser-pane">
      <div className="browser-source"><label className="field">
        <span className="field-label">{title} source</span>
        <select className="input input-sm" value={location.source ?? ''} onChange={(event) => onChange(initialLocation(event.target.value))}>
          <option value="" disabled>Select a server…</option>
          <option value="local">Local machine</option>
          {connections.map((connection) => <option key={connection.id} value={connection.id}>{connection.name}{connection.readOnly ? ' · read-only' : ''}</option>)}
        </select>
      </label>{unavailable ? <RegionToggle /> : null}</div>
      {unavailable
        ? <EmptyState message="Select a source to browse files." />
        : <BrowserContents key={location.source} id={id} location={location} target={target} onChange={onChange} />}
    </div>
  );
}

function BrowserContents({ id, location, target, onChange }: BrowserPaneProps) {
  const connections = useConnectionsSnapshot();
  const local = location.source === 'local';
  const connection = connections.find((item) => item.id === location.source);
  const destination = connections.find((item) => item.id === target.source);
  const source = useMemo(() => local ? createLocalSource() : createRemoteSource(location.source!), [local, location.source]);
  const search = useMemo(() => local ? createLocalSearch() : createRemoteSearch(location.source!), [local, location.source]);
  const [roots, setRoots] = useState<LocalRoot[]>([]);
  const { transfers } = useTransfersState();
  const finished = transfers.filter((item) => item.status === 'succeeded' && (local ? item.direction === 'download'
    : item.direction === 'remoteToRemote' ? item.targetConnectionId === location.source : item.direction === 'upload' && item.connectionId === location.source)).length;
  useEffect(() => {
    if (!local) return;
    let active = true;
    void getLocalRoots().then((items) => { if (active) setRoots(items); }).catch(() => undefined);
    return () => { active = false; };
  }, [local]);
  const changePath = useCallback((path: string) => {
    onChange({ ...location, path });
    if (location.source === 'local') setLocalPath(path);
    else setRemotePath(location.source!, path);
  }, [onChange, location]);
  const send = (from: BrowserLocation, to: BrowserLocation, paths: string[]) => {
    const request = browserTransfer(from, to, paths);
    if (request !== null) void requestTransfers(request);
  };
  const origin = `pane:${id}:${location.source}` as const;
  const otherOrigin = `pane:${id === 'left' ? 'right' : 'left'}:${target.source}` as const;
  const transfer: FilePaneTransfer = {
    label: target.source === 'local' ? 'Download' : local ? 'Upload' : 'Copy to server',
    primary: id === 'left', Icon: id === 'left' ? ArrowRightIcon : ArrowLeftIcon,
    canSend: target.source !== null && (target.source === 'local' || destination !== undefined) && target.path !== null && !destination?.readOnly && !(local && target.source === 'local'),
    unavailableReason: destination?.readOnly ? 'Destination server is read-only.' : 'Open a destination folder.',
    onSend: (paths) => send(location, target, paths),
    dragOrigin: origin, acceptsFrom: otherOrigin,
    canReceive: location.path !== null && !connection?.readOnly && !(local && target.source === 'local'),
    onReceive: (paths) => send(target, location, paths),
  };
  return <FilePane title={id === 'left' ? 'Left' : 'Right'} label={local ? 'Local' : connection?.name ?? null} sourceKey={origin}
    source={source} path={location.path} onPathChange={changePath} {...(local ? { roots } : {})}
    {...(local ? {} : { onOpenFile: (entry: import('../api/files').FileEntry) => openEditorFile(location.source!, entry.path) })}
    transfer={transfer} refreshToken={finished} search={search} readOnly={connection?.readOnly ?? false} />;
}
