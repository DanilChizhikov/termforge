import { useEffect, useRef, useState } from 'react';
import { describeError } from '../api/connections';
import {
  getSessionRecording,
  startSessionRecording,
  stopSessionRecording,
  type SessionRecording,
} from '../api/recordings';
import { showAppView, useAppView } from '../state/appView';
import { RecordingWarningDialog } from './RecordingWarningDialog';
import { formatRecordingSize } from './settings/recordingFormat';

export interface TerminalRecordingBarProps {
  terminalId: string;
  onRecordingChange: (recording: boolean) => void;
}

const POLL_INTERVAL_MILLISECONDS = 3000;

export function TerminalRecordingBar({ terminalId, onRecordingChange }: TerminalRecordingBarProps) {
  const [status, setStatus] = useState<SessionRecording | null>(null);
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const onChangeRef = useRef(onRecordingChange);
  const view = useAppView();
  const isRecording = status?.recording ?? false;

  useEffect(() => {
    onChangeRef.current = onRecordingChange;
  });

  useEffect(() => {
    onChangeRef.current(isRecording);
  }, [isRecording]);

  useEffect(() => () => onChangeRef.current(false), []);

  useEffect(() => {
    if (view !== 'workspace') {
      return undefined;
    }
    let active = true;
    getSessionRecording(terminalId)
      .then((loaded) => {
        if (active) {
          setStatus(loaded);
        }
      })
      .catch(() => undefined);
    return () => {
      active = false;
    };
  }, [terminalId, view]);

  useEffect(() => {
    if (!isRecording) {
      return undefined;
    }
    const timer = setInterval(() => {
      getSessionRecording(terminalId)
        .then(setStatus)
        .catch(() => undefined);
    }, POLL_INTERVAL_MILLISECONDS);
    return () => clearInterval(timer);
  }, [terminalId, isRecording]);

  const run = (action: (id: string) => Promise<SessionRecording>) => {
    setBusy(true);
    setError(null);
    action(terminalId)
      .then(setStatus)
      .catch((failure: unknown) => setError(describeError(failure)))
      .finally(() => {
        setBusy(false);
        setConfirming(false);
      });
  };

  return (
    <div className="terminal-meta">
      {isRecording && status !== null ? (
        <>
          <span className="status-text status-text-active">
            <span className="status-dot status-dot-sm status-dot-active" aria-hidden="true" />
            Recording · {formatRecordingSize(status.sizeBytes)} of {formatRecordingSize(status.maxBytes)}
          </span>
          <button type="button" className="btn btn-xs" disabled={busy} onClick={() => run(stopSessionRecording)}>
            Stop recording
          </button>
        </>
      ) : (
        <>
          {status?.status === 'truncated' ? (
            <span className="status-text status-text-caution">
              <span className="status-dot status-dot-sm status-dot-caution" aria-hidden="true" />
              Recording stopped at the size limit
            </span>
          ) : (
            <span>Recording off</span>
          )}
          {status === null ? null : status.available ? (
            <button type="button" className="btn btn-xs" disabled={busy} onClick={() => setConfirming(true)}>
              Record
            </button>
          ) : (
            <button type="button" className="btn btn-xs btn-ghost" onClick={() => showAppView('settings')}>
              Turn on in Settings
            </button>
          )}
        </>
      )}
      {error === null ? null : <span className="form-error">{error}</span>}
      {confirming ? (
        <RecordingWarningDialog
          title="Record this terminal?"
          confirmLabel="Start recording"
          busy={busy}
          onConfirm={() => run(startSessionRecording)}
          onCancel={() => setConfirming(false)}
        />
      ) : null}
    </div>
  );
}
