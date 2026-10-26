import type { ComponentType } from 'react';
import { activateWorkspaceTab, useActiveWorkspaceTab, type WorkspaceTabId } from '../state/workspaceTab';
import { DockerWorkspace } from './docker/DockerWorkspace';
import { EditorWorkspace } from './editor/EditorWorkspace';
import { EmptyState } from './EmptyState';
import { ContainerIcon, FileIcon, ListIcon, PulseIcon, TerminalIcon, type IconProps } from './icons/Icons';
import { MainWorkspace } from './MainWorkspace';
import { OperationHistory } from './OperationHistory';
import { ServerDiagnostics } from './ServerDiagnostics';

interface WorkspaceTabDefinition {
  id: WorkspaceTabId;
  title: string;
  emptyMessage: string | null;
  Icon: ComponentType<IconProps>;
}

const WORKSPACE_TABS: readonly WorkspaceTabDefinition[] = [
  { id: 'workspace', title: 'Workspace', emptyMessage: null, Icon: TerminalIcon },
  { id: 'editor', title: 'Editor', emptyMessage: 'No files open in the editor.', Icon: FileIcon },
  { id: 'docker', title: 'Docker', emptyMessage: 'Connect to a server to manage Docker.', Icon: ContainerIcon },
  { id: 'diagnostics', title: 'Diagnostics', emptyMessage: 'No diagnostics collected yet.', Icon: PulseIcon },
  { id: 'history', title: 'History', emptyMessage: 'No history yet.', Icon: ListIcon },
];

export function WorkspaceTabs() {
  const activeId = useActiveWorkspaceTab();

  return (
    <div className="workspace">
      <div role="tablist" aria-label="Workspace tabs" className="workspace-tabs">
        {WORKSPACE_TABS.map((tab) => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            id={`tab-${tab.id}`}
            aria-selected={tab.id === activeId}
            aria-controls={`panel-${tab.id}`}
            className="workspace-tab"
            onClick={() => activateWorkspaceTab(tab.id)}
          >
            <tab.Icon />
            {tab.title}
          </button>
        ))}
      </div>
      {WORKSPACE_TABS.map((tab) => (
        <div
          key={tab.id}
          role="tabpanel"
          id={`panel-${tab.id}`}
          aria-labelledby={`tab-${tab.id}`}
          hidden={tab.id !== activeId}
          className="tab-panel"
        >
          {renderPanel(tab, tab.id === activeId)}
        </div>
      ))}
    </div>
  );
}

function renderPanel(tab: WorkspaceTabDefinition, isActive: boolean) {
  if (tab.id === 'history') {
    return isActive ? <OperationHistory /> : null;
  }
  if (tab.id === 'editor') {
    return <EditorWorkspace />;
  }
  if (tab.id === 'docker') {
    return <DockerWorkspace active={isActive} />;
  }
  if (tab.id === 'diagnostics') {
    return isActive ? <ServerDiagnostics /> : null;
  }
  return tab.emptyMessage === null ? <MainWorkspace /> : <EmptyState message={tab.emptyMessage} />;
}
