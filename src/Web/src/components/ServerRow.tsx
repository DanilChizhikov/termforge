import { useState } from 'react';
import type { ConnectionTestResult, SavedConnection } from '../api/connections';

export type ServerStatus = ConnectionTestResult | 'testing';

export interface ServerRowProps {
  connection: SavedConnection;
  status: ServerStatus | null;
  selected: boolean;
  onSelect: () => void;
  onTest: () => void;
  onEdit: () => void;
  onRemove: () => void;
}

function describeStatus(status: ServerStatus | null): { className: string; label: string } {
  if (status === null) {
    return { className: 'status-unknown', label: 'Not tested' };
  }
  if (status === 'testing') {
    return { className: 'status-testing', label: 'Testing' };
  }
  if (status.state === 'Connected') {
    const latency = status.latencyMilliseconds === null ? '' : ` ${status.latencyMilliseconds} ms`;
    return { className: 'status-ok', label: `Connected${latency}` };
  }
  return { className: 'status-error', label: status.state };
}

export function ServerRow({ connection, status, selected, onSelect, onTest, onEdit, onRemove }: ServerRowProps) {
  const [confirming, setConfirming] = useState(false);
  const { className, label } = describeStatus(status);
  const detail = status !== null && status !== 'testing' && status.state !== 'Connected' ? status.message : null;

  return (
    <li className={selected ? 'server-row server-selected' : 'server-row'}>
      <button type="button" aria-pressed={selected} onClick={onSelect}>
        <span className={`status ${className}`} aria-hidden="true">
          {'●'}
        </span>{' '}
        {connection.name}
      </button>
      <span className="server-status">{label}</span>
      <div className="server-address">
        {connection.userName}@{connection.host}:{connection.port}
      </div>
      {connection.lastConnectedAt === null ? null : (
        <div className="server-last">Last connected {new Date(connection.lastConnectedAt).toLocaleString()}</div>
      )}
      {detail === null ? null : <div className="form-error">{detail}</div>}
      <div className="server-actions">
        <button type="button" aria-label={`Test ${connection.name}`} onClick={onTest}>
          Test
        </button>
        <button type="button" aria-label={`Edit ${connection.name}`} onClick={onEdit}>
          Edit
        </button>
        {confirming ? (
          <>
            <button type="button" aria-label={`Confirm remove ${connection.name}`} onClick={onRemove}>
              Confirm
            </button>
            <button type="button" onClick={() => setConfirming(false)}>
              Keep
            </button>
          </>
        ) : (
          <button type="button" aria-label={`Remove ${connection.name}`} onClick={() => setConfirming(true)}>
            Remove
          </button>
        )}
      </div>
    </li>
  );
}
