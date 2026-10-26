import { apiClient, type ApiClient } from './client';
import type { FileEntry } from './files';

export type FileSearchMethod = 'sftp' | 'find';

export type FileSearchOutcome = 'Completed' | 'ResultLimitReached' | 'TimeLimitReached' | 'Cancelled' | 'Failed';

export interface FileSearchQuery {
  path: string;
  pattern: string;
  maxDepth?: number;
  maxResults?: number;
  method?: FileSearchMethod;
}

export interface FileSearchSummary {
  outcome: FileSearchOutcome;
  method: 'Local' | 'Sftp' | 'RemoteFind';
  matchCount: number;
  warningCount: number;
  depthLimitReached: boolean;
  isPartial: boolean;
  message: string | null;
}

export interface FileSearchMatch {
  type: 'match';
  entry: FileEntry;
  parentPath: string;
}

export interface FileSearchWarning {
  type: 'warning';
  path: string;
  code: string;
  message: string;
}

export interface FileSearchNotice {
  type: 'notice';
  message: string;
}

export interface FileSearchCompleted {
  type: 'completed';
  summary: FileSearchSummary;
}

export type FileSearchEvent = FileSearchMatch | FileSearchWarning | FileSearchNotice | FileSearchCompleted;

export interface FileSearchSource {
  supportsRemoteFind: boolean;
  search(query: FileSearchQuery, onEvent: (event: FileSearchEvent) => void, signal: AbortSignal): Promise<void>;
}

export const DEFAULT_SEARCH_DEPTH = 8;
export const DEFAULT_SEARCH_RESULTS = 500;
export const MAX_SEARCH_DEPTH = 64;
export const MAX_SEARCH_RESULTS = 5000;

const LOCAL_SEARCH_PATH = '/api/files/local/search';

interface RawRecord {
  type?: unknown;
  entry?: Partial<FileEntry>;
  parentPath?: string;
  path?: string;
  code?: string;
  message?: string;
  summary?: Partial<FileSearchSummary>;
}

function normalizeEntry(raw: Partial<FileEntry>): FileEntry {
  return {
    name: raw.name ?? '',
    path: raw.path ?? '',
    kind: raw.kind ?? 'Other',
    size: raw.size ?? 0,
    modifiedUtc: raw.modifiedUtc ?? null,
    permissions: raw.permissions ?? null,
    linkTargetKind: raw.linkTargetKind ?? null,
    isBrokenLink: raw.isBrokenLink ?? false,
  };
}

function normalizeSummary(raw: Partial<FileSearchSummary>): FileSearchSummary {
  return {
    outcome: raw.outcome ?? 'Failed',
    method: raw.method ?? 'Local',
    matchCount: raw.matchCount ?? 0,
    warningCount: raw.warningCount ?? 0,
    depthLimitReached: raw.depthLimitReached ?? false,
    isPartial: raw.isPartial ?? true,
    message: raw.message ?? null,
  };
}

export function parseSearchRecord(line: string): FileSearchEvent | null {
  const record = JSON.parse(line) as RawRecord;
  switch (record.type) {
    case 'match':
      return { type: 'match', entry: normalizeEntry(record.entry ?? {}), parentPath: record.parentPath ?? '' };
    case 'warning':
      return { type: 'warning', path: record.path ?? '', code: record.code ?? '', message: record.message ?? '' };
    case 'notice':
      return { type: 'notice', message: record.message ?? '' };
    case 'completed':
      return { type: 'completed', summary: normalizeSummary(record.summary ?? {}) };
    default:
      return null;
  }
}

function emitLines(text: string, onEvent: (event: FileSearchEvent) => void): string {
  const lines = text.split('\n');
  const remainder = lines.pop() ?? '';
  for (const line of lines) {
    if (line.trim().length === 0) {
      continue;
    }
    const event = parseSearchRecord(line);
    if (event !== null) {
      onEvent(event);
    }
  }
  return remainder;
}

export async function readSearchStream(response: Response, onEvent: (event: FileSearchEvent) => void): Promise<void> {
  if (response.body === null) {
    emitLines(`${await response.text()}\n`, onEvent);
    return;
  }
  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let pending = '';
  for (;;) {
    const { done, value } = await reader.read();
    if (done) {
      break;
    }
    pending = emitLines(pending + decoder.decode(value, { stream: true }), onEvent);
  }
  emitLines(`${pending}${decoder.decode()}\n`, onEvent);
}

export function buildSearchUrl(basePath: string, query: FileSearchQuery): string {
  const parameters = new URLSearchParams({ path: query.path, pattern: query.pattern });
  if (query.maxDepth !== undefined) {
    parameters.set('maxDepth', String(query.maxDepth));
  }
  if (query.maxResults !== undefined) {
    parameters.set('maxResults', String(query.maxResults));
  }
  if (query.method !== undefined) {
    parameters.set('method', query.method);
  }
  return `${basePath}?${parameters.toString()}`;
}

function createSearchSource(basePath: string, supportsRemoteFind: boolean, client: ApiClient): FileSearchSource {
  return {
    supportsRemoteFind,
    async search(query, onEvent, signal) {
      const response = await client.openStream(buildSearchUrl(basePath, query), signal);
      await readSearchStream(response, onEvent);
    },
  };
}

export function createLocalSearch(client: ApiClient = apiClient): FileSearchSource {
  return createSearchSource(LOCAL_SEARCH_PATH, false, client);
}

export function createRemoteSearch(connectionId: string, client: ApiClient = apiClient): FileSearchSource {
  return createSearchSource(`/api/files/remote/${encodeURIComponent(connectionId)}/search`, true, client);
}

export function describeSearchOutcome(summary: FileSearchSummary): string {
  const found = `${summary.matchCount} ${summary.matchCount === 1 ? 'match' : 'matches'}`;
  switch (summary.outcome) {
    case 'ResultLimitReached':
      return `${found}. Stopped at the result limit.`;
    case 'TimeLimitReached':
      return `${found}. Stopped at the time limit.`;
    case 'Cancelled':
      return `${found}. Search was stopped.`;
    case 'Failed':
      return summary.message ?? 'Search failed.';
    default:
      return `${found}.`;
  }
}
