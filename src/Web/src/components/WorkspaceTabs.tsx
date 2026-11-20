import { useState, type ComponentType, type ReactNode } from 'react';
import { useConnectionName } from '../state/connections';
import { useEditorFiles } from '../state/editorFiles';
import { useFailedOperationsToday } from '../state/failedOperations';
import { useSelectedServerId } from '../state/selectedServer';
import { activateWorkspaceTab, useActiveWorkspaceTab, type WorkspaceTabId } from '../state/workspaceTab';
import { AppFooter } from './AppFooter';
import { DockerWorkspace } from './docker/DockerWorkspace';
import { EditorCloseDialog } from './editor/EditorCloseDialog';
import { EditorFileTabs } from './editor/EditorFileTabs';
import { EditorWorkspace } from './editor/EditorWorkspace';
import { ContainerIcon, ListIcon, PulseIcon, TerminalIcon, type IconProps } from './icons/Icons';
import { MainWorkspace } from './MainWorkspace';
import { OperationHistory } from './OperationHistory';
import { ServerDiagnostics } from './ServerDiagnostics';
import { NetworkDiagnosticPanel } from './NetworkDiagnosticPanel';
import { MovablePanel } from './workspace/MovablePanel';
import { useDockTab } from './workspace/useDockTab';
import { isPanelAway, usePanelPlacement } from './workspace/WorkspacePanelProvider';

type ToolTabId = Exclude<WorkspaceTabId, 'editor'>;

interface ToolTabProps {
  id: ToolTabId;
  title: string;
  context?: string | null;
  activeId: WorkspaceTabId;
  Icon: ComponentType<IconProps>;
  children?: ReactNode;
}

function ToolTab({ id, title, context = null, activeId, Icon, children }: ToolTabProps) {
  const selected = id === activeId;
  const dockTab = useDockTab();
  return (
    <span className={selected ? 'workspace-tab-item workspace-tab-item-active' : 'workspace-tab-item'}>
      <button
        type="button"
        role="tab"
        id={`tab-${id}`}
        aria-selected={selected}
        aria-controls={`panel-${id}`}
        className="workspace-tab"
        {...dockTab(id)}
        onClick={() => activateWorkspaceTab(id)}
      >
        {selected ? <Icon /> : null}
        {title}
        {context === null ? null : (
          <>
            {' '}
            <span className="workspace-tab-context">· {context}</span>
          </>
        )}
        {children}
      </button>
    </span>
  );
}

interface ToolPanelProps {
  id: ToolTabId;
  activeId: WorkspaceTabId;
  children: ReactNode;
}

function ToolPanel({ id, activeId, children }: ToolPanelProps) {
  return (
    <div
      role="tabpanel"
      id={`panel-${id}`}
      aria-labelledby={`tab-${id}`}
      hidden={id !== activeId}
      className="tab-panel"
    >
      {children}
    </div>
  );
}

interface FloatingToolPanelProps extends Omit<ToolPanelProps, 'children'> {
  title: string;
  Icon: ComponentType<IconProps>;
  keepMounted?: boolean;
  children: (visible: boolean) => ReactNode;
}

function FloatingToolPanel({ id, activeId, title, Icon, keepMounted, children }: FloatingToolPanelProps) {
  const away = isPanelAway(usePanelPlacement(id), id);
  const [wasAway, setWasAway] = useState(away);
  if (away && !wasAway) setWasAway(true);
  const visible = id === activeId || away;

  return (
    <MovablePanel id={id} title={title} Icon={Icon} active={id === activeId}>
      <ToolPanel id={id} activeId={visible ? id : activeId}>
        {visible || wasAway || keepMounted ? children(visible) : null}
      </ToolPanel>
    </MovablePanel>
  );
}

export function WorkspaceTabs() {
  const requestedId = useActiveWorkspaceTab();
  const { files } = useEditorFiles();
  const serverName = useConnectionName(useSelectedServerId());
  const failedToday = useFailedOperationsToday();
  const activeId: WorkspaceTabId = requestedId === 'editor' && files.length === 0 ? 'workspace' : requestedId;

  return (
    <div className="workspace">
      <div role="tablist" aria-label="Workspace tabs" className="workspace-tabs">
        <ToolTab id="workspace" title="Terminals & files" activeId={activeId} Icon={TerminalIcon} />
        <EditorFileTabs editorActive={activeId === 'editor'} />
        <ToolTab id="docker" title="Docker" context={serverName} activeId={activeId} Icon={ContainerIcon} />
        <ToolTab id="diagnostics" title="Diagnostics" context={serverName} activeId={activeId} Icon={PulseIcon} />
        <ToolTab id="network-diagnostics" title="Network diagnostics" activeId={activeId} Icon={PulseIcon} />
        <ToolTab id="history" title="Journal" activeId={activeId} Icon={ListIcon}>
          {failedToday === 0 ? null : (
            <>
              {' '}
              <span className="badge badge-danger">{failedToday} failed</span>
            </>
          )}
        </ToolTab>
      </div>
      <EditorCloseDialog />
      <ToolPanel id="workspace" activeId={activeId}>
        <MainWorkspace />
      </ToolPanel>
      <div className="tab-panel" hidden={activeId !== 'editor'}>
        <EditorWorkspace />
      </div>
      <FloatingToolPanel id="docker" title="Docker" Icon={ContainerIcon} activeId={activeId} keepMounted>
        {(visible) => <DockerWorkspace active={visible} />}
      </FloatingToolPanel>
      <FloatingToolPanel id="diagnostics" title="Diagnostics" Icon={PulseIcon} activeId={activeId}>
        {() => <ServerDiagnostics />}
      </FloatingToolPanel>
      <FloatingToolPanel id="network-diagnostics" title="Network diagnostics" Icon={PulseIcon} activeId={activeId}>
        {() => <NetworkDiagnosticPanel />}
      </FloatingToolPanel>
      <FloatingToolPanel id="history" title="Journal" Icon={ListIcon} activeId={activeId}>
        {() => <OperationHistory />}
      </FloatingToolPanel>
      <AppFooter />
    </div>
  );
}
