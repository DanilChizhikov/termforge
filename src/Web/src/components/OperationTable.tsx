import { canRetry, type OperationRecord } from '../api/operations';
import {
  describeOperationDetails,
  describeOperationKind,
  describeOperationStatus,
  OPERATION_STATUS_TONES,
} from './operationFormat';
import { statusDotClass, statusTextClass } from './statusTone';
import { formatClockWithSeconds } from './timeFormat';

interface OperationTableProps {
  operations: readonly OperationRecord[];
  onRetry: (operation: OperationRecord) => void;
}

export function OperationTable({ operations, onRetry }: OperationTableProps) {
  return (
    <table className="data-table operation-table">
      <thead>
        <tr>
          <th>Time</th>
          <th>Type</th>
          <th>Server</th>
          <th>Details</th>
          <th>Status</th>
          <th>
            <span className="visually-hidden">Actions</span>
          </th>
        </tr>
      </thead>
      <tbody>
        {operations.map((operation) => {
          const tone = OPERATION_STATUS_TONES[operation.status];
          const details = describeOperationDetails(operation);
          return (
            <tr key={operation.id}>
              <td className="cell-mono cell-muted" title={new Date(operation.createdAt).toLocaleString()}>
                {formatClockWithSeconds(operation.createdAt)}
              </td>
              <td>{describeOperationKind(operation.kind)}</td>
              <td className="cell-mono">{operation.server ?? 'local'}</td>
              <td className="operation-details">
                {details === '' ? null : <code>{details}</code>}
                {operation.error === null ? null : <span className="operation-error">{operation.error}</span>}
              </td>
              <td data-testid={`status-${operation.id}`}>
                <span className={statusTextClass(tone)}>
                  <span className={statusDotClass(tone, 'sm')} aria-hidden="true" />
                  {describeOperationStatus(operation)}
                </span>
              </td>
              <td className="cell-end">
                {canRetry(operation) ? (
                  <button type="button" className="btn btn-xs" onClick={() => onRetry(operation)}>
                    Retry
                  </button>
                ) : null}
              </td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}
