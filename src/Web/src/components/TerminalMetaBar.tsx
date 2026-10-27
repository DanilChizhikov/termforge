import { useEffect, useState } from 'react';
import type { TerminalSession, TerminalSize } from '../api/terminals';
import { useConnection } from '../state/connections';
import { TerminalRecordingBar } from './TerminalRecordingBar';
import { formatElapsed } from './timeFormat';

export interface TerminalMetaBarProps {
  session: TerminalSession;
  size: TerminalSize | null;
  onRecordingChange: (recording: boolean) => void;
}

const CLOCK_TICK_MILLISECONDS = 30_000;

function useNow(): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), CLOCK_TICK_MILLISECONDS);
    return () => clearInterval(timer);
  }, []);
  return now;
}

export function TerminalMetaBar({ session, size, onRecordingChange }: TerminalMetaBarProps) {
  const connection = useConnection(session.connectionId);
  const now = useNow();
  const connectedFor = formatElapsed(now - new Date(session.openedAt).getTime());

  return (
    <div className="terminal-meta">
      <span className="terminal-meta-identity">
        {connection === null ? (
          <code>{session.connectionName}</code>
        ) : (
          <>
            <code>
              {connection.userName}@{connection.host}
            </code>{' '}
            · {session.connectionName}
          </>
        )}
      </span>
      <span>Connected {connectedFor}</span>
      {size === null ? null : (
        <span>
          <code>
            {size.columns}×{size.rows}
          </code>
        </span>
      )}
      <TerminalRecordingBar key={session.id} terminalId={session.id} onRecordingChange={onRecordingChange} />
    </div>
  );
}
