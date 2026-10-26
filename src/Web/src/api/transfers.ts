import { ApiError, apiClient, type ApiClient } from './client';

export type TransferDirection = 'upload' | 'download';

export type TransferStatus = 'queued' | 'running' | 'succeeded' | 'failed' | 'cancelled';

export interface TransferRecord {
  id: string;
  connectionId: string;
  server: string;
  direction: TransferDirection;
  sourcePath: string;
  targetPath: string;
  status: TransferStatus;
  transferredBytes: number;
  totalBytes: number;
  completedFiles: number;
  totalFiles: number;
  bytesPerSecond: number;
  remainingSeconds: number | null;
  error: string | null;
  retryable: boolean;
  partialBytes: number;
}

export interface TransferStartRequest {
  connectionId: string;
  direction: TransferDirection;
  sourcePaths: string[];
  targetDirectory: string;
  overwrite: boolean;
}

export type TransferStartResult =
  | { kind: 'queued'; transfers: TransferRecord[] }
  | { kind: 'conflict'; conflicts: string[] };

const TRANSFERS_PATH = '/api/transfers';
const CONFLICT_CODE = 'TransferConflict';

export function listTransfers(client: ApiClient = apiClient): Promise<TransferRecord[]> {
  return client.get<TransferRecord[]>(TRANSFERS_PATH);
}

export async function startTransfers(
  request: TransferStartRequest,
  client: ApiClient = apiClient,
): Promise<TransferStartResult> {
  try {
    const transfers = await client.post<TransferRecord[]>(TRANSFERS_PATH, request);
    return { kind: 'queued', transfers };
  } catch (error) {
    const conflicts = readConflicts(error);
    if (conflicts !== null) {
      return { kind: 'conflict', conflicts };
    }
    throw error;
  }
}

export function cancelTransfer(id: string, client: ApiClient = apiClient): Promise<void> {
  return client.post<void>(`${TRANSFERS_PATH}/${encodeURIComponent(id)}/cancel`);
}

export function discardTransfer(id: string, client: ApiClient = apiClient): Promise<void> {
  return client.post<void>(`${TRANSFERS_PATH}/${encodeURIComponent(id)}/discard`);
}

export function clearFinishedTransfers(client: ApiClient = apiClient): Promise<void> {
  return client.post<void>(`${TRANSFERS_PATH}/clear`);
}

export function retryTransfer(id: string, client: ApiClient = apiClient): Promise<void> {
  return client.post<void>(`/api/operations/${encodeURIComponent(id)}/retry`);
}

function readConflicts(error: unknown): string[] | null {
  if (!(error instanceof ApiError) || error.status !== 409) {
    return null;
  }
  const body = error.body as { code?: string; conflicts?: unknown } | undefined;
  if (body?.code !== CONFLICT_CODE || !Array.isArray(body.conflicts)) {
    return null;
  }
  return body.conflicts.filter((item): item is string => typeof item === 'string');
}

export function isActive(transfer: TransferRecord): boolean {
  return transfer.status === 'queued' || transfer.status === 'running';
}

export function canResume(transfer: TransferRecord): boolean {
  return transfer.retryable && (transfer.status === 'failed' || transfer.status === 'cancelled');
}

const UNITS = ['B', 'KB', 'MB', 'GB', 'TB'];

export function formatBytes(bytes: number): string {
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < UNITS.length - 1) {
    value /= 1024;
    unit += 1;
  }
  return unit === 0 ? `${value} ${UNITS[unit]}` : `${value.toFixed(1)} ${UNITS[unit]}`;
}

export function formatDuration(seconds: number): string {
  const rounded = Math.max(1, Math.round(seconds));
  if (rounded < 60) {
    return `${rounded} s`;
  }
  const minutes = Math.floor(rounded / 60);
  if (minutes < 60) {
    return `${minutes} min ${rounded % 60} s`;
  }
  return `${Math.floor(minutes / 60)} h ${minutes % 60} min`;
}

export function percentOf(transfer: TransferRecord): number {
  if (transfer.totalBytes <= 0) {
    return transfer.status === 'succeeded' ? 100 : 0;
  }
  return Math.min(100, Math.round((transfer.transferredBytes / transfer.totalBytes) * 100));
}

export function describeAmount(transfer: TransferRecord): string {
  const percent = percentOf(transfer);
  const amount = `${formatBytes(transfer.transferredBytes)} of ${formatBytes(transfer.totalBytes)} · ${percent}%`;
  return transfer.totalFiles > 1 ? `${amount} · ${transfer.completedFiles} of ${transfer.totalFiles} files` : amount;
}

export function describeRate(transfer: TransferRecord): string {
  if (transfer.status !== 'running' || transfer.bytesPerSecond <= 0) {
    return '';
  }
  const rate = `${formatBytes(transfer.bytesPerSecond)}/s`;
  return transfer.remainingSeconds === null ? rate : `${rate} · ${formatDuration(transfer.remainingSeconds)} left`;
}

export function baseName(path: string): string {
  const trimmed = path.replace(/[\\/]+$/, '');
  const index = Math.max(trimmed.lastIndexOf('/'), trimmed.lastIndexOf('\\'));
  return index < 0 ? trimmed : trimmed.slice(index + 1);
}
