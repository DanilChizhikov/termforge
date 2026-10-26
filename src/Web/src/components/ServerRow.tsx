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

type StatusTone = 'idle' | 'caution' | 'ok' | 'danger';

function describeStatus(status: ServerStatus | null): { tone: StatusTone; label: string } {
  if (status === null) {
    return { tone: 'idle', label: 'Not tested' };
  }
  if (status === 'testing') {
    return { tone: 'caution', label: 'Testing' };
  }
  if (status.state === 'Connected') {
    const latency = status.latencyMilliseconds === null ? '' : ` ${status.latencyMilliseconds} ms`;
    return { tone: 'ok', label: `Connected${latency}` };
  }
  return { tone: 'danger', label: status.state };
}

export function ServerRow({ connection, status, selected, onSelect, onTest, onEdit, onRemove }: ServerRowProps) {
  const [confirming, setConfirming] = useState(false);
  const { tone, label } = describeStatus(status);
  const detail = status !== null && status !== 'testing' && status.state !== 'Connected' ? status.message : null;

  return (
    <li className={selected ? 'server-row server-row-selected' : 'server-row'}>
      <div className="server-row-main">
        <span className={`status-dot status-dot-${tone}`} aria-hidden="true" />
        <div className="server-row-text">
          <div className="server-row-title">
            <button type="button" className="server-name" aria-pressed={selected} onClick={onSelect}>
              {connection.name}
            </button>
            <span className={`server-status status-text status-text-${tone}`}>{label}</span>
          </div>
          <div className="server-address">
            {connection.userName}@{connection.host}:{connection.port}
          </div>
        </div>
      </div>
      {connection.lastConnectedAt === null ? null : (
        <div className="server-last">Last connected {new Date(connection.lastConnectedAt).toLocaleString()}</div>
      )}
      {detail === null ? null : <div className="form-error server-detail">{detail}</div>}
      <div className="server-actions">
        <button type="button" className="btn btn-xs" aria-label={`Test ${connection.name}`} onClick={onTest}>
          Test
        </button>
        <button type="button" className="btn btn-xs" aria-label={`Edit ${connection.name}`} onClick={onEdit}>
          Edit
        </button>
        {confirming ? (
          <>
            <button
              type="button"
              className="btn btn-xs btn-destructive"
              aria-label={`Confirm remove ${connection.name}`}
              onClick={onRemove}
            >
              Confirm
            </button>
            <button type="button" className="btn btn-xs btn-safe" onClick={() => setConfirming(false)}>
              Keep
            </button>
          </>
        ) : (
          <button
            type="button"
            className="btn btn-xs btn-danger-link"
            aria-label={`Remove ${connection.name}`}
            onClick={() => setConfirming(true)}
          >
            Remove
          </button>
        )}
      </div>
    </li>
  );
}
