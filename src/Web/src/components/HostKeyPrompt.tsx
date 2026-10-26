import { useCallback, useEffect, useState } from 'react';
import { ApiError } from '../api/client';
import {
  HOST_KEYS_PENDING_CHANGED,
  decideHostKey,
  listPendingHostKeys,
  type HostKeyDecision,
  type PendingHostKey,
} from '../api/hostKeys';
import { openWebSocket } from '../api/webSocket';
import { HostKeyDialog } from './HostKeyDialog';

interface RealtimeEnvelope {
  type: string;
  data: PendingHostKey[];
}

function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    const body = error.body as { detail?: string } | undefined;
    return body?.detail ?? `Request failed with status ${error.status}.`;
  }
  return 'Request failed.';
}

export function HostKeyPrompt() {
  const [pending, setPending] = useState<PendingHostKey[]>([]);
  const [error, setError] = useState<{ id: string; message: string } | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    listPendingHostKeys()
      .then((items) => {
        if (!cancelled) setPending(items);
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let socket: WebSocket;
    try {
      socket = openWebSocket('/api/ws');
    } catch {
      return undefined;
    }
    socket.onmessage = (event: MessageEvent<string>) => {
      const envelope = JSON.parse(event.data) as RealtimeEnvelope;
      if (envelope.type !== HOST_KEYS_PENDING_CHANGED) return;
      setPending(envelope.data);
    };
    return () => {
      socket.close();
    };
  }, []);

  const current = pending[0];
  const currentId = current?.id;

  const decide = useCallback(
    (decision: HostKeyDecision, confirmReplace: boolean) => {
      if (currentId === undefined) return;
      setBusy(true);
      decideHostKey(currentId, decision, confirmReplace)
        .then(() => {
          setError(null);
          setPending((items) => items.filter((item) => item.id !== currentId));
        })
        .catch((reason: unknown) => setError({ id: currentId, message: describeError(reason) }))
        .finally(() => setBusy(false));
    },
    [currentId],
  );

  if (current === undefined) {
    return null;
  }
  const message = error !== null && error.id === current.id ? error.message : null;
  return <HostKeyDialog key={current.id} pending={current} error={message} busy={busy} onDecide={decide} />;
}
