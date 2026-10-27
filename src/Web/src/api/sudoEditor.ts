import { ApiError, apiClient, type ApiClient } from './client';
import {
  toOutcome,
  type EditorDocument,
  type RollbackFileRequest,
  type SavedFile,
  type SaveFileRequest,
  type SaveOutcome,
} from './editor';

export type SudoAvailability = 'Passwordless' | 'PasswordRequired' | 'Unavailable';

export interface SudoStatus {
  availability: SudoAvailability;
  hasSavedPassword: boolean;
  message: string;
}

export interface SudoCredentials {
  password: string | null;
  rememberPassword: boolean;
}

export const NO_SUDO_PASSWORD: SudoCredentials = { password: null, rememberPassword: false };

export type SudoPasswordProblem = 'SudoPasswordRequired' | 'SudoPasswordIncorrect';

function sudoPath(connectionId: string): string {
  return `/api/editor/remote/${encodeURIComponent(connectionId)}/sudo`;
}

export function getSudoStatus(connectionId: string, client: ApiClient = apiClient): Promise<SudoStatus> {
  return client.get<SudoStatus>(`${sudoPath(connectionId)}/status`);
}

export function openRemoteFileWithSudo(
  connectionId: string,
  path: string,
  sudo: SudoCredentials,
  client: ApiClient = apiClient,
): Promise<EditorDocument> {
  return client.post<EditorDocument>(`${sudoPath(connectionId)}/open`, { path, sudo });
}

export function saveRemoteFileWithSudo(
  connectionId: string,
  request: SaveFileRequest,
  sudo: SudoCredentials,
  client: ApiClient = apiClient,
): Promise<SaveOutcome> {
  return toOutcome(client.put<SavedFile>(`${sudoPath(connectionId)}/file`, { ...request, sudo }));
}

export function readBackupWithSudo(
  connectionId: string,
  path: string,
  backupName: string,
  sudo: SudoCredentials,
  client: ApiClient = apiClient,
): Promise<EditorDocument> {
  return client.post<EditorDocument>(`${sudoPath(connectionId)}/backups/content`, { path, name: backupName, sudo });
}

export function rollbackRemoteFileWithSudo(
  connectionId: string,
  request: RollbackFileRequest,
  sudo: SudoCredentials,
  client: ApiClient = apiClient,
): Promise<SaveOutcome> {
  return toOutcome(client.post<SavedFile>(`${sudoPath(connectionId)}/rollback`, { ...request, sudo }));
}

export function forgetSudoPassword(connectionId: string, client: ApiClient = apiClient): Promise<void> {
  return client.delete<void>(`${sudoPath(connectionId)}/password`);
}

function errorCode(error: unknown): string | null {
  if (!(error instanceof ApiError)) {
    return null;
  }
  const body = error.body as { code?: string } | undefined;
  return body?.code ?? null;
}

export function sudoPasswordProblemOf(error: unknown): SudoPasswordProblem | null {
  const code = errorCode(error);
  return code === 'SudoPasswordRequired' || code === 'SudoPasswordIncorrect' ? code : null;
}

export function isPermissionDenied(error: unknown): boolean {
  return errorCode(error) === 'PermissionDenied';
}
