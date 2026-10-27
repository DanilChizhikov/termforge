import { useState } from 'react';
import type { SaveConflict } from '../../api/editor';
import { formatBytes } from '../../api/transfers';
import { WarningTriangleIcon } from '../icons/Icons';
import { formatClock } from '../timeFormat';

export interface EditorConflictNoticeProps {
  conflict: SaveConflict;
  openedSize: number | null;
  busy: boolean;
  comparing: boolean;
  onCompare: () => void;
  onReload: () => void;
  onOverwrite: () => void;
}

const PAUSED = 'Saving now would overwrite that change, so saving is paused until you choose.';

function describeTitle(conflict: SaveConflict): string {
  return conflict.currentVersion === null
    ? 'This file was deleted on the server after you opened it'
    : 'This file changed on the server after you opened it';
}

function describeServerCopy(conflict: SaveConflict, openedSize: number | null): string {
  if (conflict.currentVersion === null) {
    return 'Saving now would create it again, so saving is paused until you choose.';
  }
  const modified = formatClock(conflict.currentVersion.modifiedUtc);
  const currentSize = formatBytes(conflict.currentVersion.size);
  const sizes = openedSize === null ? currentSize : `${formatBytes(openedSize)} → ${currentSize}`;
  return `Modified at ${modified} by another process (${sizes}). ${PAUSED}`;
}

export function EditorConflictNotice({ conflict, openedSize, busy, comparing, onCompare, onReload, onOverwrite }: EditorConflictNoticeProps) {
  const [confirming, setConfirming] = useState(false);
  const deleted = conflict.currentVersion === null;

  return (
    <div role="alert" className="editor-conflict">
      <span className="editor-conflict-icon">
        <WarningTriangleIcon size={18} />
      </span>
      <div className="editor-conflict-text">
        <span className="editor-conflict-title">{describeTitle(conflict)}</span>
        <span className="editor-conflict-detail">{describeServerCopy(conflict, openedSize)}</span>
        {confirming ? (
          <span className="editor-conflict-detail">
            {deleted
              ? 'Your version will be written as a new file.'
              : 'The server copy will be replaced by your version. A backup of the server copy is taken first.'}
          </span>
        ) : null}
      </div>
      {confirming ? (
        <div className="editor-conflict-actions">
          <button type="button" className="btn btn-sm btn-safe" autoFocus disabled={busy} onClick={() => setConfirming(false)}>
            Cancel
          </button>
          <button type="button" className="btn btn-sm btn-destructive" disabled={busy} onClick={onOverwrite}>
            {deleted ? 'Recreate file' : 'Overwrite server copy'}
          </button>
        </div>
      ) : (
        <div className="editor-conflict-actions">
          <button type="button" className="btn btn-sm btn-primary" disabled={busy || deleted} onClick={onReload}>
            Reload from server
          </button>
          {deleted ? null : (
            <button type="button" className="btn btn-sm" aria-pressed={comparing} disabled={busy} onClick={onCompare}>
              Compare
            </button>
          )}
          <button type="button" className="btn btn-sm btn-danger-link" disabled={busy} onClick={() => setConfirming(true)}>
            {deleted ? 'Recreate…' : 'Overwrite…'}
          </button>
        </div>
      )}
    </div>
  );
}
