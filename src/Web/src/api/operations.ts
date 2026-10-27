import { apiClient, type ApiClient } from './client';

export type OperationStatus = 'queued' | 'running' | 'succeeded' | 'failed' | 'cancelled';

export const OPERATION_STATUSES: readonly OperationStatus[] = [
  'queued',
  'running',
  'succeeded',
  'failed',
  'cancelled',
];

export interface OperationRecord {
  id: string;
  kind: string;
  server: string | null;
  status: OperationStatus;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  updatedAt: string;
  details: Record<string, string>;
  error: string | null;
  retryable: boolean;
}

export interface OperationPage {
  items: OperationRecord[];
  total: number;
  offset: number;
  limit: number;
}

export interface OperationFilters {
  kind: string;
  status: OperationStatus | '';
  server: string;
  from: string;
  to: string;
}

export const EMPTY_FILTERS: OperationFilters = { kind: '', status: '', server: '', from: '', to: '' };

export const PAGE_SIZE = 20;

export function buildListPath(filters: OperationFilters, offset: number, limit: number = PAGE_SIZE): string {
  const search = new URLSearchParams();
  if (filters.kind.trim() !== '') search.set('kind', filters.kind.trim());
  if (filters.status !== '') search.set('status', filters.status);
  if (filters.server.trim() !== '') search.set('server', filters.server.trim());
  if (filters.from !== '') search.set('from', new Date(filters.from).toISOString());
  if (filters.to !== '') search.set('to', new Date(filters.to).toISOString());
  search.set('offset', String(offset));
  search.set('limit', String(limit));
  return `/api/operations?${search.toString()}`;
}

export function listOperations(
  filters: OperationFilters,
  offset: number,
  client: ApiClient = apiClient,
): Promise<OperationPage> {
  return client.get<OperationPage>(buildListPath(filters, offset));
}

export function retryOperation(id: string, client: ApiClient = apiClient): Promise<void> {
  return client.post<void>(`/api/operations/${id}/retry`);
}

export function matchesFilters(operation: OperationRecord, filters: OperationFilters): boolean {
  const kind = filters.kind.trim();
  if (kind !== '' && operation.kind !== kind && !operation.kind.startsWith(`${kind}.`)) return false;
  if (filters.status !== '' && operation.status !== filters.status) return false;
  if (filters.server.trim() !== '' && operation.server !== filters.server.trim()) return false;
  const created = Date.parse(operation.createdAt);
  if (filters.from !== '' && created < new Date(filters.from).getTime()) return false;
  if (filters.to !== '' && created > new Date(filters.to).getTime()) return false;
  return true;
}

export function canRetry(operation: OperationRecord): boolean {
  return operation.retryable && (operation.status === 'failed' || operation.status === 'cancelled');
}
