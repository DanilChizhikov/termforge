import { canRetry, type OperationRecord, type OperationStatus } from '../api/operations';
import type { StatusTone } from './statusTone';

const STATUS_TONES: Record<OperationStatus, StatusTone> = {
  queued: 'idle',
  running: 'active',
  succeeded: 'ok',
  failed: 'danger',
  cancelled: 'idle',
};

interface OperationTableProps {
  operations: readonly OperationRecord[];
  onRetry: (operation: OperationRecord) => void;
}

export function OperationTable({ operations, onRetry }: OperationTableProps) {
  return (
    <table className="data-table">
      <thead>
        <tr>
          <th>Kind</th>
          <th>Server</th>
          <th>Status</th>
          <th>Created</th>
          <th>Finished</th>
          <th>Error</th>
          <th>Actions</th>
        </tr>
      </thead>
      <tbody>
        {operations.map((operation) => (
          <tr key={operation.id}>
            <td className="cell-mono">{operation.kind}</td>
            <td className="cell-mono">{operation.server ?? ''}</td>
            <td data-testid={`status-${operation.id}`}>
              <span className={`status-text status-text-${STATUS_TONES[operation.status]}`}>
                <span className={`status-dot status-dot-sm status-dot-${STATUS_TONES[operation.status]}`} aria-hidden="true" />
                {operation.status}
              </span>
            </td>
            <td className="cell-muted">{new Date(operation.createdAt).toLocaleString()}</td>
            <td className="cell-muted">
              {operation.finishedAt === null ? '' : new Date(operation.finishedAt).toLocaleString()}
            </td>
            <td className="status-text-danger">{operation.error ?? ''}</td>
            <td>
              {canRetry(operation) ? (
                <button type="button" className="btn btn-xs btn-safe" onClick={() => onRetry(operation)}>
                  Retry
                </button>
              ) : null}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
