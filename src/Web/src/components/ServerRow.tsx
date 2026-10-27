import { useState } from 'react';
import type { ConnectionTestResult, ConnectionTestState, SavedConnection } from '../api/connections';
import { statusDotClass, type StatusTone } from './statusTone';
import { formatRelativeTime } from './timeFormat';

export type ServerStatus = ConnectionTestResult | 'testing';

export interface ServerRowProps {
  connection: SavedConnection;
  status: ServerStatus | null;
  sessionCount: number;
  selected: boolean;
  onSelect: () => void;
  onTest: () => void;
  onEdit: () => void;
  onRemove: () => void;
}

interface StatusDescription {
  tone: StatusTone;
  label: string;
  inline: boolean;
}

const FAILURE_LABELS: Record<Exclude<ConnectionTestState, 'Connected' | 'Unreachable' | 'Timeout'>, string> = {
  AuthenticationFailed: 'Authentication failed',
  HostKeyRejected: 'Host key rejected · blocked',
  CredentialsUnavailable: 'Credentials unavailable',
  Failed: 'Connection failed',
};

function describeOffline(connection: SavedConnection): string {
  if (connection.lastConnectedAt === null) {
    return 'Offline';
  }
  return `Offline · last seen ${formatRelativeTime(connection.lastConnectedAt)}`;
}

function describeStatus(connection: SavedConnection, status: ServerStatus | null): StatusDescription {
  if (status === null) {
    return { tone: 'idle', label: 'Not tested', inline: true };
  }
  if (status === 'testing') {
    return { tone: 'active', label: 'Testing…', inline: false };
  }
  if (status.state === 'Connected') {
    const latency = status.latencyMilliseconds === null ? '' : ` · ${status.latencyMilliseconds} ms`;
    return { tone: 'ok', label: `Connected${latency}`, inline: true };
  }
  if (status.state === 'Unreachable' || status.state === 'Timeout') {
    return { tone: 'idle', label: describeOffline(connection), inline: false };
  }
  return { tone: 'danger', label: FAILURE_LABELS[status.state], inline: false };
}

function formatAddress(connection: SavedConnection): string {
  const port = connection.port === 22 ? '' : `:${connection.port}`;
  return `${connection.userName}@${connection.host}${port}`;
}

export function ServerRow({ connection, status, sessionCount, selected, onSelect, onTest, onEdit, onRemove }: ServerRowProps) {
  const [confirming, setConfirming] = useState(false);
  const { tone, label, inline } = describeStatus(connection, status);
  const detail = status !== null && status !== 'testing' && status.state !== 'Connected' ? status.message : null;
  const latency = status !== null && status !== 'testing' && status.state === 'Connected' ? status.latencyMilliseconds : null;

  return (
    <li className={selected ? 'server-row server-row-selected' : 'server-row'}>
      <div className="server-row-main">
        <span className={statusDotClass(tone)} aria-hidden="true" />
        <div className="server-row-text">
          <button type="button" className="server-name" aria-pressed={selected} onClick={onSelect}>
            {connection.name}
          </button>
          {inline ? (
            <span className="server-subtitle">
              <code>{formatAddress(connection)}</code>
              {latency === null ? null : ` · ${latency} ms`}
              <span className="visually-hidden"> {label}</span>
            </span>
          ) : (
            <span className={`server-subtitle status-text-${tone}`}>{label}</span>
          )}
        </div>
        {sessionCount === 0 ? null : (
          <span className="server-sessions" aria-label={`${sessionCount} open sessions`}>
            {sessionCount}
          </span>
        )}
      </div>
      {detail === null ? null : <div className="form-error server-detail">{detail}</div>}
      <div className="server-actions">
        <button type="button" className="btn btn-xs btn-ghost" aria-label={`Test ${connection.name}`} onClick={onTest}>
          Test
        </button>
        <button type="button" className="btn btn-xs btn-ghost" aria-label={`Edit ${connection.name}`} onClick={onEdit}>
          Edit
        </button>
        {confirming ? (
          <>
            <button type="button" className="btn btn-xs btn-safe" autoFocus onClick={() => setConfirming(false)}>
              Keep
            </button>
            <button
              type="button"
              className="btn btn-xs btn-destructive"
              aria-label={`Confirm remove ${connection.name}`}
              onClick={onRemove}
            >
              Remove
            </button>
          </>
        ) : (
          <button
            type="button"
            className="btn btn-xs btn-ghost btn-danger-link"
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
