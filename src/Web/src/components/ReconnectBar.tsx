import { useEffect, useRef, useState } from 'react';
import { getConnection, type ReconnectMode } from '../api/connections';
import { RefreshIcon } from './icons/Icons';

export interface ReconnectBarProps {
  connectionId: string;
  reason: string | null;
  wasConnected: boolean;
  onReconnect: () => void;
}

export function ReconnectBar({ connectionId, reason, wasConnected, onReconnect }: ReconnectBarProps) {
  const [mode, setMode] = useState<ReconnectMode | null>(null);
  const reconnectRef = useRef(onReconnect);
  const startedAutomatically = useRef(false);

  useEffect(() => {
    reconnectRef.current = onReconnect;
  });

  useEffect(() => {
    let cancelled = false;
    getConnection(connectionId)
      .then((connection) => {
        if (!cancelled) {
          setMode(connection.reconnectMode);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setMode('Off');
        }
      });
    return () => {
      cancelled = true;
    };
  }, [connectionId]);

  useEffect(() => {
    if (mode === 'Automatic' && wasConnected && !startedAutomatically.current) {
      startedAutomatically.current = true;
      reconnectRef.current();
    }
  }, [mode, wasConnected]);

  const canReconnectManually = mode === 'Manual' || (mode === 'Automatic' && !wasConnected);

  return (
    <p role="status" className="terminal-disconnected">
      <span>Disconnected: {reason}</span>
      {canReconnectManually ? (
        <button type="button" className="btn btn-sm" onClick={onReconnect}>
          <RefreshIcon size={13} />
          Reconnect
        </button>
      ) : null}
    </p>
  );
}
