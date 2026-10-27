import { useMemo } from 'react';
import type { TransferDirection } from '../api/transfers';
import { useLocalPath, useRemotePath } from '../state/filePaths';
import { useConnectionName } from '../state/connections';
import { useSelectedServerId } from '../state/selectedServer';
import { requestTransfers, useTransfersState } from '../state/transfers';
import type { FilePaneTransfer } from './filePaneTransfer';
import { ArrowLeftIcon, ArrowRightIcon } from './icons/Icons';

function startTransfer(serverId: string | null, direction: TransferDirection, sourcePaths: string[], targetDirectory: string | null): void {
  if (serverId === null || targetDirectory === null || sourcePaths.length === 0) {
    return;
  }
  void requestTransfers({ connectionId: serverId, direction, sourcePaths, targetDirectory, overwrite: false });
}

export function useLocalPaneTransfer(): FilePaneTransfer {
  const serverId = useSelectedServerId();
  const localPath = useLocalPath();
  const remotePath = useRemotePath(serverId ?? '');
  const canSend = serverId !== null && remotePath !== null;
  const canReceive = serverId !== null && localPath !== null;
  const serverName = useConnectionName(serverId);
  return useMemo(
    () => ({
      label: serverName === null ? 'Upload' : `Upload to ${serverName}`,
      primary: true,
      Icon: ArrowRightIcon,
      canSend,
      unavailableReason: 'Open a remote folder to upload into.',
      onSend: (paths) => startTransfer(serverId, 'upload', paths, remotePath),
      dragOrigin: 'local',
      acceptsFrom: 'remote',
      canReceive,
      onReceive: (paths) => startTransfer(serverId, 'download', paths, localPath),
    }),
    [serverId, serverName, localPath, remotePath, canSend, canReceive],
  );
}

export function useRemotePaneTransfer(serverId: string): FilePaneTransfer {
  const localPath = useLocalPath();
  const remotePath = useRemotePath(serverId);
  const canSend = localPath !== null;
  const canReceive = remotePath !== null;
  return useMemo(
    () => ({
      label: 'Download',
      primary: false,
      Icon: ArrowLeftIcon,
      canSend,
      unavailableReason: 'Open a local folder to download into.',
      onSend: (paths) => startTransfer(serverId, 'download', paths, localPath),
      dragOrigin: 'remote',
      acceptsFrom: 'local',
      canReceive,
      onReceive: (paths) => startTransfer(serverId, 'upload', paths, remotePath),
    }),
    [serverId, localPath, remotePath, canSend, canReceive],
  );
}

export function useFinishedTransferCount(direction: TransferDirection, serverId: string | null): number {
  const { transfers } = useTransfersState();
  return transfers.filter(
    (transfer) =>
      transfer.status === 'succeeded' &&
      transfer.direction === direction &&
      (serverId === null || transfer.connectionId === serverId),
  ).length;
}
