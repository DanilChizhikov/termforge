import { apiClient, type ApiClient } from './client';

export type MonitoringState = 'Connected' | 'ConnectionFailed' | 'CredentialsUnavailable';

export interface Metric<TValue> {
  available: boolean;
  value: TValue | null;
  reason: string | null;
}

export interface MemoryUsage {
  usedBytes: number;
  totalBytes: number;
}

export interface DiskUsage {
  freeBytes: number;
  totalBytes: number;
  mountPoint: string;
}

export interface ServerMetrics {
  connectionId: string;
  state: MonitoringState;
  collectedAt: string;
  message: string | null;
  uptimeSeconds: Metric<number>;
  cpuPercent: Metric<number>;
  memory: Metric<MemoryUsage>;
  disk: Metric<DiskUsage>;
}

function isMetric(value: unknown): boolean {
  return typeof value === 'object' && value !== null && 'available' in value;
}

export function isServerMetrics(value: unknown): value is ServerMetrics {
  if (typeof value !== 'object' || value === null) {
    return false;
  }
  const candidate = value as Record<string, unknown>;
  return (
    typeof candidate.state === 'string' &&
    isMetric(candidate.uptimeSeconds) &&
    isMetric(candidate.cpuPercent) &&
    isMetric(candidate.memory) &&
    isMetric(candidate.disk)
  );
}

export async function getServerMetrics(
  connectionId: string,
  client: ApiClient = apiClient,
): Promise<ServerMetrics> {
  const path = `/api/connections/${encodeURIComponent(connectionId)}/metrics`;
  const metrics = await client.get<unknown>(path);
  if (!isServerMetrics(metrics)) {
    throw new Error('Unexpected metrics response.');
  }
  return metrics;
}
