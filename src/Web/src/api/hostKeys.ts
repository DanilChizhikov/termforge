import { apiClient, type ApiClient } from './client';

export const HOST_KEYS_PENDING_CHANGED = 'hostKeys.pendingChanged';

export type HostKeyStatus = 'unknown' | 'changed';

export type HostKeyDecision = 'acceptOnce' | 'acceptAndSave' | 'reject' | 'replace';

export interface HostKeyInfo {
  algorithm: string;
  fingerprint: string;
}

export interface PendingHostKey {
  id: string;
  host: string;
  port: number;
  status: HostKeyStatus;
  presentedKey: HostKeyInfo;
  knownKeys: HostKeyInfo[];
  expiresAt: string;
}

const PENDING_PATH = '/api/host-keys/pending';

export function listPendingHostKeys(client: ApiClient = apiClient): Promise<PendingHostKey[]> {
  return client.get<PendingHostKey[]>(PENDING_PATH);
}

export function decideHostKey(
  id: string,
  decision: HostKeyDecision,
  confirmReplace = false,
  client: ApiClient = apiClient,
): Promise<void> {
  return client.post<void>(`${PENDING_PATH}/${id}/decision`, { decision, confirmReplace });
}
