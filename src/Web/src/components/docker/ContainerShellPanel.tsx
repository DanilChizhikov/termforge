import { useCallback, useEffect, useRef, useState } from 'react';
import { openContainerShell } from '../../api/docker';
import { closeTerminal, type TerminalSession, type TerminalSize, type TerminalState } from '../../api/terminals';
import { CloseIcon } from '../icons/Icons';
import { TerminalView } from '../TerminalView';

export interface ContainerShellPanelProps {
  connectionId: string;
  container: string;
  onClose: () => void;
}

export function ContainerShellPanel({ connectionId, container, onClose }: ContainerShellPanelProps) {
  const [state, setState] = useState<TerminalState>('connecting');
  const [reason, setReason] = useState<string | null>(null);
  const sessionRef = useRef<string | null>(null);

  const open = useCallback(
    (size: TerminalSize) => openContainerShell(connectionId, container, size),
    [connectionId, container],
  );

  const handleOpened = useCallback((session: TerminalSession) => {
    sessionRef.current = session.id;
  }, []);

  const handleStateChange = useCallback((next: TerminalState, nextReason: string | null) => {
    setState(next);
    setReason(nextReason);
  }, []);

  useEffect(
    () => () => {
      const sessionId = sessionRef.current;
      if (sessionId !== null) {
        closeTerminal(sessionId).catch(() => undefined);
      }
    },
    [],
  );

  return (
    <section aria-label={`Shell in ${container}`} className="docker-panel">
      <div className="docker-panel-bar">
        <span className="docker-panel-title">
          Shell · <code>{container}</code>
        </span>
        <span className="text-muted">bash, falling back to sh</span>
        <span className="docker-panel-spacer" />
        <span className={`status-text status-text-${SHELL_TONES[state]}`}>
          <span className={`status-dot status-dot-sm status-dot-${SHELL_TONES[state]}`} aria-hidden="true" />
          {state === 'disconnected' ? (reason ?? 'Disconnected') : SHELL_LABELS[state]}
        </span>
        <button type="button" className="icon-btn" aria-label="Close shell" onClick={onClose}>
          <CloseIcon />
        </button>
      </div>
      <div className="docker-shell-body">
        <TerminalView
          connectionId={connectionId}
          active
          open={open}
          onOpened={handleOpened}
          onStateChange={handleStateChange}
        />
      </div>
    </section>
  );
}

const SHELL_TONES: Record<TerminalState, string> = {
  connecting: 'active',
  connected: 'ok',
  disconnected: 'idle',
};

const SHELL_LABELS: Record<TerminalState, string> = {
  connecting: 'Connecting',
  connected: 'Connected',
  disconnected: 'Disconnected',
};
