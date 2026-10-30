import { useCallback, useEffect, useState } from 'react';
import { AI_APPROVALS_CHANGED, decideAiApproval, listAiApprovals, type AiApproval } from '../api/aiBridge';
import { describeError } from '../api/connections';
import { openWebSocket } from '../api/webSocket';
import { AiApprovalDialog } from './AiApprovalDialog';

interface RealtimeEnvelope {
  type: string;
  data: AiApproval[];
}

export function AiApprovalPrompt() {
  const [pending, setPending] = useState<AiApproval[]>([]);
  const [error, setError] = useState<{ id: string; message: string } | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    listAiApprovals()
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
      if (envelope.type !== AI_APPROVALS_CHANGED) return;
      setPending(envelope.data);
    };
    return () => {
      socket.close();
    };
  }, []);

  const current = pending[0];
  const currentId = current?.id;

  const decide = useCallback(
    (approve: boolean) => {
      if (currentId === undefined) return;
      setBusy(true);
      decideAiApproval(currentId, approve)
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
  return (
    <AiApprovalDialog
      key={current.id}
      approval={current}
      queued={pending.length}
      error={message}
      busy={busy}
      onDecide={decide}
    />
  );
}
