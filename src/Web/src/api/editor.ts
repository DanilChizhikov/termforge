import { ApiError, apiClient, type ApiClient } from './client';
import { requestConfirmationToken } from './confirmations';

export interface FileVersion {
  modifiedUtc: string;
  size: number;
  sha256: string;
}

export interface EditorDocument {
  path: string;
  content: string;
  hasByteOrderMark: boolean;
  version: FileVersion;
  permissions: string;
  ownerId?: number;
}

export interface FileBackup {
  name: string;
  createdAtUtc: string;
}

export interface SavedFile {
  version: FileVersion;
  backupName: string | null;
  strategy: 'AtomicRename' | 'RenameFallback' | 'InPlace';
  warnings: string[];
  operationId: string;
  document?: EditorDocument | null;
}

export interface SaveConflict {
  code: 'Conflict';
  detail: string;
  currentVersion: FileVersion | null;
  currentContent: string | null;
  currentHasByteOrderMark: boolean;
  operationId: string;
}

export type SaveOutcome = { kind: 'saved'; saved: SavedFile } | { kind: 'conflict'; conflict: SaveConflict };

export interface SaveFileRequest {
  path: string;
  content: string;
  hasByteOrderMark: boolean;
  expectedVersion: FileVersion | null;
}

export interface RollbackFileRequest {
  path: string;
  backupName: string;
  expectedVersion: FileVersion | null;
}

const HTTP_CONFLICT = 409;

function basePath(connectionId: string): string {
  return `/api/editor/remote/${encodeURIComponent(connectionId)}`;
}

function pathQuery(path: string): string {
  return `path=${encodeURIComponent(path)}`;
}

function isConflict(error: unknown): error is ApiError {
  if (!(error instanceof ApiError) || error.status !== HTTP_CONFLICT) {
    return false;
  }
  const body = error.body as { code?: string } | undefined;
  return body?.code === 'Conflict';
}

export async function toOutcome(request: Promise<SavedFile>): Promise<SaveOutcome> {
  try {
    const saved = await request;
    return { kind: 'saved', saved };
  } catch (error) {
    if (isConflict(error)) {
      return { kind: 'conflict', conflict: error.body as SaveConflict };
    }
    throw error;
  }
}

export function openRemoteFile(connectionId: string, path: string, client: ApiClient = apiClient): Promise<EditorDocument> {
  return client.get<EditorDocument>(`${basePath(connectionId)}/file?${pathQuery(path)}`);
}

export function saveRemoteFile(
  connectionId: string,
  request: SaveFileRequest,
  client: ApiClient = apiClient,
): Promise<SaveOutcome> {
  return toOutcome(client.put<SavedFile>(`${basePath(connectionId)}/file`, request));
}

export function listBackups(connectionId: string, path: string, client: ApiClient = apiClient): Promise<FileBackup[]> {
  return client.get<FileBackup[]>(`${basePath(connectionId)}/backups?${pathQuery(path)}`);
}

export function readBackup(
  connectionId: string,
  path: string,
  backupName: string,
  client: ApiClient = apiClient,
): Promise<EditorDocument> {
  const query = `${pathQuery(path)}&name=${encodeURIComponent(backupName)}`;
  return client.get<EditorDocument>(`${basePath(connectionId)}/backups/content?${query}`);
}

export async function rollbackRemoteFile(
  connectionId: string,
  request: RollbackFileRequest,
  client: ApiClient = apiClient,
): Promise<SaveOutcome> {
  const confirmationToken = await requestConfirmationToken({ operation: 'FileRollback', connectionId, target: request.path }, client);
  return toOutcome(client.post<SavedFile>(`${basePath(connectionId)}/rollback`, { ...request, confirmationToken }));
}

export function describeEditorError(error: unknown): string {
  if (error instanceof ApiError) {
    const body = error.body as { detail?: string } | undefined;
    return body?.detail ?? `Request failed with status ${error.status}.`;
  }
  return 'Request failed.';
}
