import type { ConnectionTestResult, ConnectionTestState, SavedConnection } from '../api/connections';
import type { StatusTone } from './statusTone';
import { formatRelativeTime } from './timeFormat';

export type ServerStatus = ConnectionTestResult | 'testing';

export interface StatusDescription {
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

export function describeServerStatus(connection: SavedConnection, status: ServerStatus | null): StatusDescription {
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
