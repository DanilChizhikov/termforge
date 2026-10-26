export interface RecordingWarningDialogProps {
  title: string;
  confirmLabel: string;
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

export function RecordingWarningDialog({ title, confirmLabel, busy = false, onConfirm, onCancel }: RecordingWarningDialogProps) {
  return (
    <div className="dialog-backdrop">
      <div role="dialog" aria-modal="true" aria-labelledby="recording-warning-title" className="dialog-card">
        <div className="dialog-header">
          <h2 id="recording-warning-title" className="dialog-title">
            {title}
          </h2>
          <p className="text-secondary">
            Everything the server prints in the terminal is saved on this computer until you delete it or the
            retention limits remove it. Keystrokes are never recorded.
          </p>
        </div>
        <div className="dialog-body">
          <p className="notice notice-caution" data-testid="recording-secrets-warning">
            Recordings may contain secrets: passwords, tokens or keys that commands print to the screen.
          </p>
        </div>
        <div className="dialog-footer">
          <button type="button" className="btn" onClick={onConfirm} disabled={busy}>
            {confirmLabel}
          </button>
          <button type="button" className="btn btn-safe" autoFocus onClick={onCancel}>
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
}
