import { useCallback, useEffect, useState } from 'react';
import { describeError } from '../../api/connections';
import { deleteRecording, listRecordings, type RecordingSummary } from '../../api/recordings';
import { RefreshIcon } from '../icons/Icons';
import { RecordingPlayer } from './RecordingPlayer';
import { formatDayAndClock } from '../timeFormat';
import {
  formatRecordingSize,
  recordingDuration,
  recordingStatusView,
  recordingTitle,
} from './recordingFormat';

export interface RecordingListProps {
  version: number;
}

export function RecordingList({ version }: RecordingListProps) {
  const [recordings, setRecordings] = useState<RecordingSummary[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [replaying, setReplaying] = useState<RecordingSummary | null>(null);
  const [deleting, setDeleting] = useState<RecordingSummary | null>(null);
  const [refreshCount, setRefreshCount] = useState(0);

  const refresh = useCallback(() => setRefreshCount((count) => count + 1), []);

  useEffect(() => {
    let active = true;
    listRecordings()
      .then((loaded) => {
        if (active) {
          setRecordings(loaded);
          setError(null);
        }
      })
      .catch((failure: unknown) => {
        if (active) {
          setError(describeError(failure));
        }
      });
    return () => {
      active = false;
    };
  }, [version, refreshCount]);

  const confirmDelete = () => {
    if (deleting === null) {
      return;
    }
    const target = deleting;
    deleteRecording(target.id)
      .then(() => {
        setRecordings((current) => current?.filter((recording) => recording.id !== target.id) ?? null);
        setError(null);
      })
      .catch((failure: unknown) => setError(describeError(failure)))
      .finally(() => setDeleting(null));
  };

  return (
    <section className="settings-section" aria-labelledby="recordings-title">
      <div className="settings-section-header">
        <span className="settings-row-text">
          <h2 id="recordings-title" className="settings-section-title">
            Terminal recordings
          </h2>
          <span className="settings-row-hint">
            Output only, in asciicast v2 format. Recordings may contain secrets printed in the terminal.
          </span>
        </span>
        <button type="button" className="icon-btn" aria-label="Refresh recordings" onClick={refresh}>
          <RefreshIcon />
        </button>
      </div>
      {error === null ? null : (
        <p role="alert" className="form-error">
          {error}
        </p>
      )}
      {recordings === null ? null : recordings.length === 0 ? (
        <p className="empty-state">No recordings yet.</p>
      ) : (
        <div className="settings-card settings-table-card">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Terminal</th>
                <th scope="col">Started</th>
                <th scope="col">Length</th>
                <th scope="col" className="cell-end">
                  Size
                </th>
                <th scope="col">Status</th>
                <th scope="col">
                  <span className="visually-hidden">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {recordings.map((recording) => {
                const status = recordingStatusView(recording.status);
                const title = recordingTitle(recording);
                const isRecording = recording.status === 'recording';
                return (
                  <tr key={recording.id}>
                    <td>
                      <code className="mono">{title}</code>
                    </td>
                    <td className="cell-muted" title={new Date(recording.startedAt).toLocaleString()}>
                      {formatDayAndClock(recording.startedAt)}
                    </td>
                    <td className="cell-muted">{recordingDuration(recording) ?? '—'}</td>
                    <td className="cell-end cell-mono">{formatRecordingSize(recording.sizeBytes)}</td>
                    <td>
                      <span className={`status-text ${status.textClassName}`}>
                        <span className={`status-dot status-dot-sm ${status.dotClassName}`} aria-hidden="true" />
                        {status.label}
                      </span>
                    </td>
                    <td>
                      <span className="settings-table-actions">
                        <button
                          type="button"
                          className="btn btn-xs"
                          aria-label={`Replay ${title}`}
                          onClick={() => setReplaying(recording)}
                        >
                          Replay
                        </button>
                        <button
                          type="button"
                          className="btn btn-xs btn-danger-link"
                          aria-label={`Delete ${title}`}
                          disabled={isRecording}
                          onClick={() => setDeleting(recording)}
                        >
                          Delete
                        </button>
                      </span>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      {replaying === null ? null : <RecordingPlayer recording={replaying} onClose={() => setReplaying(null)} />}
      {deleting === null ? null : (
        <div className="dialog-backdrop">
          <div role="dialog" aria-modal="true" aria-labelledby="delete-recording-title" className="dialog-card">
            <div className="dialog-header">
              <h2 id="delete-recording-title" className="dialog-title">
                Delete the recording of <code>{recordingTitle(deleting)}</code>?
              </h2>
              <p className="text-secondary">The recorded output is removed from this computer and cannot be restored.</p>
            </div>
            <div className="dialog-footer">
              <button type="button" className="btn btn-destructive" onClick={confirmDelete}>
                Delete recording
              </button>
              <button type="button" className="btn btn-safe" autoFocus onClick={() => setDeleting(null)}>
                Cancel
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
