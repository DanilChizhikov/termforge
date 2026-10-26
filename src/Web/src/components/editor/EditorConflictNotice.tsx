import { useState } from 'react';
import type { SaveConflict } from '../../api/editor';
import { ShieldAlertIcon } from '../icons/Icons';

export interface EditorConflictNoticeProps {
  conflict: SaveConflict;
  busy: boolean;
  comparing: boolean;
  onCompare: () => void;
  onReload: () => void;
  onOverwrite: () => void;
}

function describeServerCopy(conflict: SaveConflict): string {
  if (conflict.currentVersion === null) {
    return 'The file no longer exists on the server. Saving now would create it again, so saving is paused until you choose.';
  }
  const modified = new Date(conflict.currentVersion.modifiedUtc).toLocaleString();
  return `Server copy: ${conflict.currentVersion.size} bytes, modified ${modified}. Saving now would overwrite that change, so saving is paused until you choose.`;
}

export function EditorConflictNotice({ conflict, busy, comparing, onCompare, onReload, onOverwrite }: EditorConflictNoticeProps) {
  const [confirming, setConfirming] = useState(false);
  const deleted = conflict.currentVersion === null;

  return (
    <div role="alert" className="editor-conflict">
      <ShieldAlertIcon size={18} />
      <div className="editor-conflict-text">
        <span className="editor-conflict-title">{conflict.detail}</span>
        <span className="editor-conflict-detail">{describeServerCopy(conflict)}</span>
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
          <button type="button" className="btn btn-sm btn-safe" disabled={busy} onClick={() => setConfirming(false)}>
            Cancel
          </button>
          <button type="button" className="btn btn-sm btn-destructive" disabled={busy} onClick={onOverwrite}>
            {deleted ? 'Recreate file' : 'Overwrite server copy'}
          </button>
        </div>
      ) : (
        <div className="editor-conflict-actions">
          {deleted ? null : (
            <button type="button" className="btn btn-sm" aria-pressed={comparing} disabled={busy} onClick={onCompare}>
              Compare
            </button>
          )}
          <button type="button" className="btn btn-sm" disabled={busy || deleted} onClick={onReload}>
            Reload from server
          </button>
          <button type="button" className="btn btn-sm" disabled={busy} onClick={() => setConfirming(true)}>
            {deleted ? 'Recreate…' : 'Overwrite…'}
          </button>
        </div>
      )}
    </div>
  );
}
