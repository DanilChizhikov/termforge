import { useEffect, useState } from 'react';
import { describeEditorError, listBackups, type FileBackup } from '../../api/editor';
import { useConnectionName } from '../../state/connections';
import { LoadingState } from '../LoadingState';
import { formatDayAndClock } from '../timeFormat';

export interface EditorVersionsProps {
  connectionId: string;
  path: string;
  refreshToken: number;
  canRollBack: boolean;
  busy: boolean;
  saveNote?: string | null;
  onCompare: (backup: FileBackup) => void;
  onRollBack: (backup: FileBackup) => void;
}

interface BackupsState {
  key: string;
  backups: FileBackup[];
  error: string | null;
}

const SAVE_STEPS = [
  'Check the server copy is unchanged since you opened it',
  'Back up the current file next to it',
  'Write your version to a temporary file beside it',
  'Atomically replace the original, keeping its permissions',
] as const;

export function EditorVersions({
  connectionId,
  path,
  refreshToken,
  canRollBack,
  busy,
  saveNote = null,
  onCompare,
  onRollBack,
}: EditorVersionsProps) {
  const [state, setState] = useState<BackupsState | null>(null);
  const [confirming, setConfirming] = useState<string | null>(null);
  const serverName = useConnectionName(connectionId);
  const requestKey = `${connectionId}|${path}|${refreshToken}`;

  useEffect(() => {
    let cancelled = false;
    listBackups(connectionId, path)
      .then((backups) => {
        if (!cancelled) {
          setState({ key: requestKey, backups, error: null });
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setState({ key: requestKey, backups: [], error: describeEditorError(error) });
        }
      });
    return () => {
      cancelled = true;
    };
  }, [connectionId, path, requestKey]);

  const loaded = state !== null && state.key === requestKey ? state : null;

  return (
    <aside aria-label="Save plan and versions" className="editor-aside">
      <div className="editor-aside-section">
        <span className="editor-aside-title">How this file will be saved</span>
        <ol className="editor-steps">
          {SAVE_STEPS.map((step, index) => (
            <li key={step}>
              <span className="editor-step-number">{index + 1}</span>
              <span>{step}</span>
            </li>
          ))}
        </ol>
        {saveNote === null ? null : <p className="field-hint">{saveNote}</p>}
      </div>
      <div className="editor-aside-section">
        <span className="editor-aside-title">Versions</span>
        {loaded === null ? <LoadingState message="Loading backups…" /> : null}
        {loaded?.error != null ? <p className="form-error">{loaded.error}</p> : null}
        {loaded !== null && loaded.error === null && loaded.backups.length === 0 ? (
          <p className="text-muted">No backups yet. One is taken before every save.</p>
        ) : null}
        {!canRollBack && loaded !== null && loaded.backups.length > 0 ? (
          <p className="field-hint">Save or discard your changes before rolling back.</p>
        ) : null}
        <ul className="editor-versions" aria-label="Backups">
          {(loaded?.backups ?? []).map((backup) => (
            <li key={backup.name} className="editor-version">
              <span className="editor-version-text" title={backup.name}>
                <span>{formatDayAndClock(backup.createdAtUtc)}</span>
                {confirming === backup.name ? (
                  <span className="editor-version-note">
                    Restore <code>{path}</code> on {serverName ?? 'this server'}?
                  </span>
                ) : (
                  <span className="editor-version-note">Saved from TermForge · backup</span>
                )}
              </span>
              {confirming === backup.name ? (
                <span className="editor-version-actions">
                  <button type="button" className="btn btn-xs btn-safe" onClick={() => setConfirming(null)}>
                    Cancel
                  </button>
                  <button
                    type="button"
                    className="btn btn-xs btn-destructive"
                    disabled={busy}
                    onClick={() => {
                      setConfirming(null);
                      onRollBack(backup);
                    }}
                  >
                    Restore
                  </button>
                </span>
              ) : (
                <span className="editor-version-actions">
                  <button type="button" className="btn btn-xs" onClick={() => onCompare(backup)}>
                    Compare
                  </button>
                  <button
                    type="button"
                    className="btn btn-xs"
                    disabled={!canRollBack || busy}
                    onClick={() => setConfirming(backup.name)}
                  >
                    Roll back
                  </button>
                </span>
              )}
            </li>
          ))}
        </ul>
      </div>
    </aside>
  );
}
