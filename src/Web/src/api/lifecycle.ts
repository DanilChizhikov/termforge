import { apiClient, type ApiClient } from './client';
import type { TransferRecord } from './transfers';

export type ExitState = 'running' | 'waitingForTransfers' | 'shuttingDown';

export interface UnsavedFile {
  connectionId: string;
  server: string;
  path: string;
}

export interface ActiveTerminal {
  connectionId: string;
  server: string;
  sessionNumber: number;
  openedAt: string;
}

export interface PendingAttention {
  state: ExitState;
  transfers: TransferRecord[];
  unsavedFiles: UnsavedFile[];
  terminalSessions: ActiveTerminal[];
  idleShutdownMinutes: number;
}

export interface ExitAccepted {
  state: ExitState;
}

const EXIT_PATH = '/api/lifecycle/exit';
const ATTENTION_PATH = '/api/lifecycle/attention';

export function getPendingAttention(client: ApiClient = apiClient): Promise<PendingAttention> {
  return client.get<PendingAttention>(ATTENTION_PATH);
}

export function requestExit(waitForTransfers: boolean, client: ApiClient = apiClient): Promise<ExitAccepted> {
  return client.post<ExitAccepted>(EXIT_PATH, { waitForTransfers });
}

export function cancelExit(client: ApiClient = apiClient): Promise<void> {
  return client.delete<void>(EXIT_PATH);
}

export function attentionCount(attention: PendingAttention): number {
  const groups = [attention.transfers.length, attention.unsavedFiles.length, attention.terminalSessions.length];
  return groups.filter((count) => count > 0).length;
}
