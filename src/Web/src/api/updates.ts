import { apiClient, type ApiClient } from './client';

export type UpdateState = 'notchecked' | 'uptodate' | 'updateavailable' | 'checkfailed';

export interface UpdateStatus {
  state: UpdateState;
  currentVersion: string;
  latestVersion: string | null;
  releaseUrl: string | null;
  checkedAt: string | null;
}

export function getUpdateStatus(client: ApiClient = apiClient): Promise<UpdateStatus> {
  return client.get<UpdateStatus>('/api/updates');
}

export function checkForUpdates(client: ApiClient = apiClient): Promise<UpdateStatus> {
  return client.post<UpdateStatus>('/api/updates/check');
}
