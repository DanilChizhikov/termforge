import { apiClient, type ApiClient } from './client';

export type CapabilityStatus = 'Available' | 'Unavailable' | 'Unknown';

export type CapabilityReportState = 'Completed' | 'ConnectionFailed' | 'CredentialsUnavailable';

export interface Capability {
  id: string;
  name: string;
  status: CapabilityStatus;
  reason: string;
}

export interface CapabilityReport {
  connectionId: string;
  state: CapabilityReportState;
  checkedAt: string;
  message: string | null;
  capabilities: Capability[];
}

function capabilitiesPath(connectionId: string): string {
  return `/api/connections/${encodeURIComponent(connectionId)}/capabilities`;
}

export function getCapabilities(
  connectionId: string,
  client: ApiClient = apiClient,
): Promise<CapabilityReport> {
  return client.get<CapabilityReport>(capabilitiesPath(connectionId));
}

export function refreshCapabilities(
  connectionId: string,
  client: ApiClient = apiClient,
): Promise<CapabilityReport> {
  return client.post<CapabilityReport>(`${capabilitiesPath(connectionId)}/refresh`);
}
