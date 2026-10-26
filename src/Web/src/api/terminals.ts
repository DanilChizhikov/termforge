import { apiClient, type ApiClient } from './client';
import { buildWebSocketUrl, type LocationLike } from './webSocket';

export interface TerminalSession {
  id: string;
  connectionId: string;
  connectionName: string;
  sessionNumber: number;
  openedAt: string;
}

export interface TerminalSize {
  columns: number;
  rows: number;
}

export type TerminalState = 'connecting' | 'connected' | 'disconnected';

export type TerminalServerMessage =
  | { type: 'status'; state: 'connected' }
  | { type: 'status'; state: 'disconnected'; reason: string | null }
  | { type: 'error'; message: string };

const TERMINALS_PATH = '/api/terminals';

export function openTerminal(
  connectionId: string,
  size: TerminalSize,
  client: ApiClient = apiClient,
): Promise<TerminalSession> {
  return client.post<TerminalSession>(TERMINALS_PATH, {
    connectionId,
    columns: size.columns,
    rows: size.rows,
  });
}

export function closeTerminal(id: string, client: ApiClient = apiClient): Promise<void> {
  return client.delete<void>(`${TERMINALS_PATH}/${encodeURIComponent(id)}`);
}

export function terminalSocketUrl(id: string, location?: LocationLike): string {
  const path = `${TERMINALS_PATH}/${encodeURIComponent(id)}/ws`;
  return location === undefined ? buildWebSocketUrl(path) : buildWebSocketUrl(path, {}, location);
}

export function resizeMessage(size: TerminalSize): string {
  return JSON.stringify({ type: 'resize', columns: size.columns, rows: size.rows });
}

export function parseServerMessage(text: string): TerminalServerMessage | null {
  let parsed: unknown;
  try {
    parsed = JSON.parse(text);
  } catch {
    return null;
  }
  if (typeof parsed !== 'object' || parsed === null) {
    return null;
  }
  const message = parsed as Record<string, unknown>;
  if (message['type'] === 'error' && typeof message['message'] === 'string') {
    return { type: 'error', message: message['message'] };
  }
  if (message['type'] !== 'status') {
    return null;
  }
  if (message['state'] === 'connected') {
    return { type: 'status', state: 'connected' };
  }
  if (message['state'] === 'disconnected') {
    const reason = typeof message['reason'] === 'string' ? message['reason'] : null;
    return { type: 'status', state: 'disconnected', reason };
  }
  return null;
}
