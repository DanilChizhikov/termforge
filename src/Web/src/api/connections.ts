import { ApiError, apiClient, type ApiClient } from './client';

export type AuthMethod = 'KeyFile' | 'Agent' | 'Password';

export const AUTH_METHODS: readonly AuthMethod[] = ['Agent', 'KeyFile', 'Password'];

export interface SavedConnection {
  id: string;
  name: string;
  groupName: string | null;
  host: string;
  port: number;
  userName: string;
  authMethod: AuthMethod;
  privateKeyPath: string | null;
  jumpHostId: string | null;
  sshAlias: string | null;
  hasPassword: boolean;
  lastConnectedAt: string | null;
}

export interface ConnectionInput {
  name: string;
  groupName: string;
  host: string;
  port: number;
  userName: string;
  authMethod: AuthMethod;
  privateKeyPath: string;
  jumpHostId: string;
  password: string;
}

export type ConnectionTestState =
  | 'Connected'
  | 'AuthenticationFailed'
  | 'Unreachable'
  | 'Timeout'
  | 'HostKeyRejected'
  | 'CredentialsUnavailable'
  | 'Failed';

export interface ConnectionTestResult {
  state: ConnectionTestState;
  testedAt: string;
  message: string | null;
  detail: string | null;
  latencyMilliseconds: number | null;
  lastConnectedAt: string | null;
}

export interface SshConfigIssue {
  filePath: string;
  line: number;
  directive: string;
  reason: string;
  affectedAliases: string[];
}

export interface SshConfigImportResult {
  configPath: string;
  created: number;
  updated: number;
  issues: SshConfigIssue[];
}

const CONNECTIONS_PATH = '/api/connections';
const GROUPS_PATH = '/api/connection-groups';

export function emptyInput(): ConnectionInput {
  return {
    name: '',
    groupName: '',
    host: '',
    port: 22,
    userName: '',
    authMethod: 'Agent',
    privateKeyPath: '',
    jumpHostId: '',
    password: '',
  };
}

export function inputFrom(connection: SavedConnection): ConnectionInput {
  return {
    name: connection.name,
    groupName: connection.groupName ?? '',
    host: connection.host,
    port: connection.port,
    userName: connection.userName,
    authMethod: connection.authMethod,
    privateKeyPath: connection.privateKeyPath ?? '',
    jumpHostId: connection.jumpHostId ?? '',
    password: '',
  };
}

function toBody(input: ConnectionInput): Record<string, unknown> {
  const body: Record<string, unknown> = {
    name: input.name,
    groupName: input.groupName === '' ? null : input.groupName,
    host: input.host,
    port: input.port,
    userName: input.userName,
    authMethod: input.authMethod,
    privateKeyPath: input.authMethod === 'KeyFile' ? input.privateKeyPath : null,
    jumpHostId: input.jumpHostId === '' ? null : input.jumpHostId,
  };
  if (input.authMethod === 'Password' && input.password !== '') {
    body['password'] = input.password;
  }
  return body;
}

export function listConnections(client: ApiClient = apiClient): Promise<SavedConnection[]> {
  return client.get<SavedConnection[]>(CONNECTIONS_PATH);
}

export function createConnection(input: ConnectionInput, client: ApiClient = apiClient): Promise<SavedConnection> {
  return client.post<SavedConnection>(CONNECTIONS_PATH, toBody(input));
}

export function updateConnection(
  id: string,
  input: ConnectionInput,
  client: ApiClient = apiClient,
): Promise<SavedConnection> {
  return client.put<SavedConnection>(`${CONNECTIONS_PATH}/${id}`, toBody(input));
}

export function removeConnection(id: string, client: ApiClient = apiClient): Promise<void> {
  return client.delete<void>(`${CONNECTIONS_PATH}/${id}`);
}

export function testConnection(id: string, client: ApiClient = apiClient): Promise<ConnectionTestResult> {
  return client.post<ConnectionTestResult>(`${CONNECTIONS_PATH}/${id}/test`);
}

export function importSshConfig(client: ApiClient = apiClient): Promise<SshConfigImportResult> {
  return client.post<SshConfigImportResult>(`${CONNECTIONS_PATH}/import/ssh-config`);
}

export function renameGroup(name: string, newName: string, client: ApiClient = apiClient): Promise<void> {
  return client.put<void>(`${GROUPS_PATH}/${encodeURIComponent(name)}`, { newName });
}

export function removeGroup(name: string, client: ApiClient = apiClient): Promise<void> {
  return client.delete<void>(`${GROUPS_PATH}/${encodeURIComponent(name)}`);
}

export function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    const body = error.body as { detail?: string } | undefined;
    return body?.detail ?? `Request failed with status ${error.status}.`;
  }
  return 'Request failed.';
}
