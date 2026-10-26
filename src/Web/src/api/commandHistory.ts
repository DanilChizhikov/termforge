import { apiClient, type ApiClient } from './client';

export interface CommandHistoryEntry {
  id: string;
  connectionId: string;
  command: string;
  executedAt: string;
  exitCode: number | null;
  isPinned: boolean;
}

export type ShellName = 'bash' | 'zsh';

export interface ShellIntegration {
  shell: ShellName;
  snippet: string;
}

const CONNECTIONS_PATH = '/api/connections';
const HISTORY_PATH = '/api/command-history';

export function listCommandHistory(
  connectionId: string,
  search: string,
  client: ApiClient = apiClient,
): Promise<CommandHistoryEntry[]> {
  const trimmed = search.trim();
  const query = trimmed === '' ? '' : `?search=${encodeURIComponent(trimmed)}`;
  return client.get<CommandHistoryEntry[]>(`${CONNECTIONS_PATH}/${connectionId}/history${query}`);
}

export function pinCommand(id: string, pinned: boolean, client: ApiClient = apiClient): Promise<void> {
  return client.put<void>(`${HISTORY_PATH}/${id}/pin`, { pinned });
}

export function deleteCommand(id: string, client: ApiClient = apiClient): Promise<void> {
  return client.delete<void>(`${HISTORY_PATH}/${id}`);
}

export function clearCommandHistory(connectionId: string, client: ApiClient = apiClient): Promise<void> {
  return client.delete<void>(`${CONNECTIONS_PATH}/${connectionId}/history`);
}

export function getShellIntegration(shell: ShellName, client: ApiClient = apiClient): Promise<ShellIntegration> {
  return client.get<ShellIntegration>(`${HISTORY_PATH}/integration/${shell}`);
}

export function isInsertable(entry: CommandHistoryEntry): boolean {
  return !entry.command.includes('\n');
}
