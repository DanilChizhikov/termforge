import { Group, Panel, Separator } from 'react-resizable-panels';
import { AppHeader } from './components/AppHeader';
import { CollapsibleRegion } from './components/CollapsibleRegion';
import { HostKeyPrompt } from './components/HostKeyPrompt';
import { ServerSidebar } from './components/ServerSidebar';
import { SessionBanner } from './components/SessionBanner';
import { SettingsView } from './components/settings/SettingsView';
import { WorkspaceRestoreNotice } from './components/WorkspaceRestoreNotice';
import { WorkspaceTabs } from './components/WorkspaceTabs';
import { useAppView } from './state/appView';
import { useConnections } from './state/connections';
import { usePersistentLayout } from './state/workspaceLayout';

export function App() {
  const view = useAppView();
  useConnections();
  const layout = usePersistentLayout('app-layout');

  return (
    <div className="app">
      <AppHeader />
      <SessionBanner />
      <WorkspaceRestoreNotice />
      <div className="app-view" hidden={view !== 'workspace'}>
        <Group orientation="horizontal" id="app-layout" className="group" {...layout}>
          <CollapsibleRegion id="servers" title="Servers" defaultSize="264px" minSize="200px" collapsedLayout="rail">
            <ServerSidebar />
          </CollapsibleRegion>
          <Separator className="separator separator-vertical" aria-label="Resize panels" />
          <Panel id="main" minSize="30%">
            <WorkspaceTabs />
          </Panel>
        </Group>
      </div>
      {view === 'settings' ? <SettingsView /> : null}
      <HostKeyPrompt />
    </div>
  );
}
