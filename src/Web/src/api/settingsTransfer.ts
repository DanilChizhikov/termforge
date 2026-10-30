import { apiClient, type ApiClient } from './client';

export interface SettingsSections {
  connections: boolean;
  preferences: boolean;
  workspace: boolean;
  pinnedCommands: boolean;
}

export interface SettingsTransferSummary {
  connections: number;
  groups: number;
  pinnedCommands: number;
}

export interface SettingsExportRequest {
  sections: SettingsSections;
}

export interface ExportedFile {
  fileName: string;
  contentBase64: string;
}

export interface SettingsExport {
  file: ExportedFile;
  key: string;
  secretCount: number;
}

export type ImportMode = 'merge' | 'replace';

export type ConflictResolution = 'keepExisting' | 'useImported' | 'keepBoth';

export type ConnectionImportAction = 'add' | 'update' | 'unchanged' | 'conflict' | 'skipped';

export interface SettingsImportRequest {
  file: string;
  key: string;
  mode: ImportMode;
  sections: SettingsSections;
  resolutions: Record<string, ConflictResolution>;
  confirmReplace: boolean;
}

export interface ConnectionImportStep {
  importedId: string;
  name: string;
  host: string;
  userName: string;
  port: number;
  action: ConnectionImportAction;
  resolution: ConflictResolution | null;
  localName: string;
  changes: string[];
  hasPassword: boolean;
  hasSudoPassword: boolean;
}

export interface RemovedConnection {
  id: string;
  name: string;
  host: string;
}

export interface WorkspaceImportSummary {
  terminalTabs: number;
  editorTabs: number;
  remotePaths: number;
}

export interface SettingsImportPreview {
  exportedAt: string;
  sectionsInFile: SettingsSections;
  sectionsToApply: SettingsSections;
  connections: ConnectionImportStep[];
  removals: RemovedConnection[];
  preferenceChanges: string[];
  workspace: WorkspaceImportSummary | null;
  pinnedCommands: number;
  secrets: { passwords: number; sudoPasswords: number };
  warnings: string[];
}

export interface SettingsImportResult {
  added: number;
  updated: number;
  unchanged: number;
  kept: number;
  removed: number;
  secretsStored: number;
  preferencesImported: boolean;
  workspaceImported: boolean;
  pinnedCommandsAdded: number;
  pinnedCommandsSkipped: number;
  warnings: string[];
}

export const SETTINGS_FILE_EXTENSION = '.tfsettings';

const ROUTE = '/api/settings-transfer';

export function getTransferSummary(client: ApiClient = apiClient): Promise<SettingsTransferSummary> {
  return client.get<SettingsTransferSummary>(`${ROUTE}/summary`);
}

export function exportSettings(request: SettingsExportRequest, client: ApiClient = apiClient): Promise<SettingsExport> {
  return client.post<SettingsExport>(`${ROUTE}/export`, request);
}

export function previewImport(request: SettingsImportRequest, client: ApiClient = apiClient): Promise<SettingsImportPreview> {
  return client.post<SettingsImportPreview>(`${ROUTE}/import/preview`, request);
}

export function applyImport(request: SettingsImportRequest, client: ApiClient = apiClient): Promise<SettingsImportResult> {
  return client.post<SettingsImportResult>(`${ROUTE}/import`, request);
}

export function decodeBase64(encoded: string): Uint8Array<ArrayBuffer> {
  const binary = atob(encoded);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes;
}

export function encodeBase64(bytes: Uint8Array): string {
  const chunkSize = 0x8000;
  let binary = '';
  for (let offset = 0; offset < bytes.length; offset += chunkSize) {
    const chunk = bytes.subarray(offset, offset + chunkSize);
    binary += String.fromCharCode(...chunk);
  }
  return btoa(binary);
}
