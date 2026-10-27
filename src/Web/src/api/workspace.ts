import { apiClient, type ApiClient } from './client';
import type { FileVersion } from './editor';

export interface WorkspaceTerminalTab {
  key: string;
  connectionId: string;
  connectionName: string;
}

export interface WorkspaceRemotePath {
  connectionId: string;
  path: string;
}

export interface WorkspacePanelLayout {
  groupId: string;
  sizes: Record<string, number>;
}

export interface WorkspaceEditorTab {
  connectionId: string;
  path: string;
}

export interface WorkspaceState {
  selectedConnectionId: string | null;
  activeView: string | null;
  terminalTabs: WorkspaceTerminalTab[];
  activeTerminalKey: string | null;
  localPath: string | null;
  remotePaths: WorkspaceRemotePath[];
  panelLayouts: WorkspacePanelLayout[];
  collapsedPanels: string[];
  editorTabs: WorkspaceEditorTab[];
  activeEditorTab: WorkspaceEditorTab | null;
}

export interface WorkspaceEditorDraft {
  connectionId: string;
  path: string;
  content: string;
  baseVersion: FileVersion | null;
}

export interface WorkspaceSnapshot {
  state: WorkspaceState;
  drafts: WorkspaceEditorDraft[];
}

export interface WorkspaceWarning {
  code: 'Corrupted' | 'UnsupportedVersion';
  message: string;
}

export interface WorkspaceRestore extends WorkspaceSnapshot {
  warning: WorkspaceWarning | null;
}

const WORKSPACE_PATH = '/api/workspace';

export function getWorkspace(client: ApiClient = apiClient): Promise<WorkspaceRestore> {
  return client.get<WorkspaceRestore>(WORKSPACE_PATH);
}

export function saveWorkspace(snapshot: WorkspaceSnapshot, client: ApiClient = apiClient): Promise<void> {
  return client.put<void>(WORKSPACE_PATH, snapshot);
}
