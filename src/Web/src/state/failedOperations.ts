import { useEffect, useState } from 'react';
import { EMPTY_FILTERS, listOperations, type OperationRecord } from '../api/operations';
import { openWebSocket } from '../api/webSocket';

const OPERATION_CHANGED = 'operation.changed';

interface RealtimeEnvelope {
  type: string;
  data: OperationRecord;
}

function startOfToday(): string {
  const now = new Date();
  const midnight = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  return midnight.toISOString();
}

function loadFailedToday(): Promise<number> {
  const filters = { ...EMPTY_FILTERS, status: 'failed' as const, from: startOfToday() };
  return listOperations(filters, 0).then((page) => page.total);
}

export function useFailedOperationsToday(): number {
  const [count, setCount] = useState(0);

  useEffect(() => {
    let cancelled = false;
    const refresh = () => {
      loadFailedToday()
        .then((total) => {
          if (!cancelled) {
            setCount(total);
          }
        })
        .catch(() => undefined);
    };
    refresh();
    let socket: WebSocket | null = null;
    try {
      socket = openWebSocket('/api/ws');
      socket.onmessage = (event: MessageEvent<string>) => {
        const envelope = JSON.parse(event.data) as RealtimeEnvelope;
        if (envelope.type === OPERATION_CHANGED) {
          refresh();
        }
      };
    } catch {
      socket = null;
    }
    return () => {
      cancelled = true;
      socket?.close();
    };
  }, []);

  return count;
}
