import { apiClient, type ApiClient } from './client';

export const AI_APPROVALS_CHANGED = 'aiBridge.approvalsChanged';

export interface AiBridgeSettings {
  enabled: boolean;
  requireApproval: boolean;
  hasToken: boolean;
  mcpUrl: string;
}

export interface AiBridgeTokenInfo {
  id: string;
  name: string;
  createdAt: string;
}

export interface AiBridgeToken extends AiBridgeTokenInfo {
  token: string;
  mcpUrl: string;
}

export type AiActionKind = 'command' | 'terminalInput' | 'containerAction' | 'fileWrite';

export interface AiFileChange {
  path: string;
  before: string;
  after: string;
}

export interface AiApproval {
  id: string;
  connectionId: string;
  serverName: string;
  kind: AiActionKind;
  summary: string;
  fileChange: AiFileChange | null;
  expiresAt: string;
}

const BRIDGE_PATH = '/api/ai-bridge';

export function getAiBridgeSettings(client: ApiClient = apiClient): Promise<AiBridgeSettings> {
  return client.get<AiBridgeSettings>(`${BRIDGE_PATH}/settings`);
}

export function updateAiBridgeSettings(
  settings: Pick<AiBridgeSettings, 'enabled' | 'requireApproval'>,
  client: ApiClient = apiClient,
): Promise<AiBridgeSettings> {
  return client.put<AiBridgeSettings>(`${BRIDGE_PATH}/settings`, settings);
}

export function listAiBridgeTokens(client: ApiClient = apiClient): Promise<AiBridgeTokenInfo[]> {
  return client.get<AiBridgeTokenInfo[]>(`${BRIDGE_PATH}/tokens`);
}

export function createAiBridgeToken(name: string, client: ApiClient = apiClient): Promise<AiBridgeToken> {
  return client.post<AiBridgeToken>(`${BRIDGE_PATH}/tokens`, { name });
}

export function revokeAiBridgeToken(id: string, client: ApiClient = apiClient): Promise<void> {
  return client.delete<void>(`${BRIDGE_PATH}/tokens/${encodeURIComponent(id)}`);
}

export function listAiApprovals(client: ApiClient = apiClient): Promise<AiApproval[]> {
  return client.get<AiApproval[]>(`${BRIDGE_PATH}/approvals`);
}

export function decideAiApproval(id: string, approve: boolean, client: ApiClient = apiClient): Promise<void> {
  return client.post<void>(`${BRIDGE_PATH}/approvals/${id}`, { approve });
}

export function mcpAddCommand(mcpUrl: string, token: string): string {
  return `claude mcp add --transport http termforge ${mcpUrl} --header "Authorization: Bearer ${token}"`;
}
