import { apiClient, type ApiClient } from './client';

export type DangerousOperation =
  | 'RemoteDelete'
  | 'ContainerStop'
  | 'ContainerRestart'
  | 'FileRollback'
  | 'TransferOverwrite';

export interface ConfirmationScope {
  operation: DangerousOperation;
  connectionId: string;
  target: string;
}

export interface ConfirmationGrant {
  token: string;
  expiresAt: string;
  operation: DangerousOperation;
  serverName: string;
  target: string;
}

const CONFIRMATIONS_PATH = '/api/confirmations';

export function requestConfirmation(scope: ConfirmationScope, client: ApiClient = apiClient): Promise<ConfirmationGrant> {
  return client.post<ConfirmationGrant>(CONFIRMATIONS_PATH, scope);
}

export async function requestConfirmationToken(scope: ConfirmationScope, client: ApiClient = apiClient): Promise<string> {
  const grant = await requestConfirmation(scope, client);
  return grant.token;
}
