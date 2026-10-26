import type { PendingConflict } from '../state/transfers';

export interface TransferConflictDialogProps {
  pending: PendingConflict;
  onOverwrite: () => void;
  onCancel: () => void;
}

export function TransferConflictDialog({ pending, onOverwrite, onCancel }: TransferConflictDialogProps) {
  const count = pending.conflicts.length;
  return (
    <div className="dialog-backdrop">
      <div role="dialog" aria-modal="true" aria-labelledby="transfer-conflict-title" className="dialog-card">
        <div className="dialog-header">
          <h2 id="transfer-conflict-title" className="dialog-title">
            {count === 1 ? 'Replace existing item?' : `Replace ${count} existing items?`}
          </h2>
          <p className="text-secondary">
            The target already contains {count === 1 ? 'an item' : 'items'} with the same name. Nothing is transferred
            until you decide.
          </p>
        </div>
        <div className="dialog-body">
          <ul aria-label="Existing items" className="transfer-conflict-list">
            {pending.conflicts.map((path) => (
              <li key={path}>
                <code>{path}</code>
              </li>
            ))}
          </ul>
        </div>
        <div className="dialog-footer">
          <button type="button" className="btn btn-destructive" onClick={onOverwrite}>
            Overwrite
          </button>
          <button type="button" className="btn btn-safe" autoFocus onClick={onCancel}>
            Keep existing
          </button>
        </div>
      </div>
    </div>
  );
}
