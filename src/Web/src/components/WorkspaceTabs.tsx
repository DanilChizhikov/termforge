import { useState } from 'react';
import { EmptyState } from './EmptyState';
import { MainWorkspace } from './MainWorkspace';
import { OperationHistory } from './OperationHistory';
import { ServerDiagnostics } from './ServerDiagnostics';

interface WorkspaceTabDefinition {
  id: string;
  title: string;
  emptyMessage: string | null;
}

const WORKSPACE_TABS: readonly WorkspaceTabDefinition[] = [
  { id: 'workspace', title: 'Workspace', emptyMessage: null },
  { id: 'editor', title: 'Editor', emptyMessage: 'No files open in the editor.' },
  { id: 'docker', title: 'Docker', emptyMessage: 'Connect to a server to manage Docker.' },
  { id: 'diagnostics', title: 'Diagnostics', emptyMessage: 'No diagnostics collected yet.' },
  { id: 'history', title: 'History', emptyMessage: 'No history yet.' },
];

export function WorkspaceTabs() {
  const [activeId, setActiveId] = useState(WORKSPACE_TABS[0]?.id ?? 'workspace');

  return (
    <div className="workspace">
      <div role="tablist" aria-label="Workspace tabs" className="tab-strip">
        {WORKSPACE_TABS.map((tab) => (
          <button
            key={tab.id}
            type="button"
            role="tab"
            id={`tab-${tab.id}`}
            aria-selected={tab.id === activeId}
            aria-controls={`panel-${tab.id}`}
            className={tab.id === activeId ? 'tab tab-active' : 'tab'}
            onClick={() => setActiveId(tab.id)}
          >
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
  if (tab.id === 'diagnostics') {
    return isActive ? <ServerDiagnostics /> : null;
  }
  return tab.emptyMessage === null ? <MainWorkspace /> : <EmptyState message={tab.emptyMessage} />;
}
