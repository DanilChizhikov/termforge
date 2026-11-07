import { apiClient } from './client';

export interface InstallationStatus {
  state: 'idle' | 'preparing' | 'prepared' | 'waitingForTransfers' | 'restarting' | 'failed';
  version: string | null;
  error: string | null;
  supported: boolean;
  unavailableReason: string | null;
  previousVersion: string | null;
  readyToRestart: boolean;
  operation: 'update' | 'rollback' | null;
}

export const getInstallationStatus = () => apiClient.get<InstallationStatus>('/api/updates/installation');
export const prepareInstallation = (rollback: boolean) => apiClient.post<void>(`/api/updates/${rollback ? 'rollback' : 'prepare'}`);
export const cancelInstallation = () => apiClient.post<void>('/api/updates/cancel');
export const installPreparedUpdate = (waitForTransfers: boolean, saveCurrentData: boolean) =>
  apiClient.post<InstallationStatus>('/api/updates/install', { waitForTransfers, saveCurrentData });
