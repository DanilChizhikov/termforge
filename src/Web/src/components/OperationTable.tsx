import { canRetry, type OperationRecord } from '../api/operations';

interface OperationTableProps {
  operations: readonly OperationRecord[];
  onRetry: (operation: OperationRecord) => void;
}

export function OperationTable({ operations, onRetry }: OperationTableProps) {
  return (
    <table className="operation-table">
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
            <td>{operation.kind}</td>
            <td>{operation.server ?? ''}</td>
            <td data-testid={`status-${operation.id}`}>{operation.status}</td>
            <td>{new Date(operation.createdAt).toLocaleString()}</td>
            <td>{operation.finishedAt === null ? '' : new Date(operation.finishedAt).toLocaleString()}</td>
            <td>{operation.error ?? ''}</td>
            <td>
              {canRetry(operation) ? (
                <button type="button" onClick={() => onRetry(operation)}>
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
