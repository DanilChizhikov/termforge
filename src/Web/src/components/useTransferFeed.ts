import { useEffect, useState } from 'react';
import { ApiError } from '../api/client';
import { listTransfers, type TransferRecord } from '../api/transfers';
import { openWebSocket } from '../api/webSocket';
import { removeTransfer, replaceTransfers, reportTransferError, upsertTransfer } from '../state/transfers';

const TRANSFER_CHANGED = 'transfer.changed';
const TRANSFER_REMOVED = 'transfer.removed';

type TransferEnvelope =
  | { type: typeof TRANSFER_CHANGED; data: TransferRecord }
  | { type: typeof TRANSFER_REMOVED; data: { id: string } }
  | { type: string; data: unknown };

export function useTransferFeed(): boolean {
  const [live, setLive] = useState(false);

  useEffect(() => {
    let cancelled = false;
    listTransfers()
      .then((transfers) => {
        if (!cancelled) replaceTransfers(transfers);
      })
      .catch((error: unknown) => {
        const sessionMissing = error instanceof ApiError && error.isSessionMissing;
        if (!cancelled && !sessionMissing) reportTransferError(error);
      });

    let socket: WebSocket;
    try {
      socket = openWebSocket('/api/ws');
    } catch {
      return () => {
        cancelled = true;
      };
    }
    socket.onopen = () => setLive(true);
    socket.onclose = () => setLive(false);
    socket.onmessage = (event: MessageEvent<string>) => {
      const envelope = JSON.parse(event.data) as TransferEnvelope;
      if (envelope.type === TRANSFER_CHANGED) {
        upsertTransfer(envelope.data as TransferRecord);
      } else if (envelope.type === TRANSFER_REMOVED) {
        removeTransfer((envelope.data as { id: string }).id);
      }
    };
    return () => {
      cancelled = true;
      socket.onclose = null;
      socket.close();
    };
  }, []);

  return live;
}
