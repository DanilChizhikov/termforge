import { Group, Panel, Separator } from 'react-resizable-panels';
import { AppHeader } from './components/AppHeader';
import { CollapsibleRegion } from './components/CollapsibleRegion';
import { HostKeyPrompt } from './components/HostKeyPrompt';
import { ServerSidebar } from './components/ServerSidebar';
import { SessionBanner } from './components/SessionBanner';
import { WorkspaceTabs } from './components/WorkspaceTabs';

export function App() {
  return (
    <div className="app">
      <AppHeader />
      <SessionBanner />
      <Group orientation="horizontal" id="app-layout" className="group">
        <CollapsibleRegion id="servers" title="Servers" defaultSize="20%" minSize="10%">
          <ServerSidebar />
        </CollapsibleRegion>
        <Separator className="separator separator-vertical" />
        <Panel id="main" defaultSize="80%" minSize="30%">
          <WorkspaceTabs />
        </Panel>
      </Group>
      <HostKeyPrompt />
    </div>
  );
}
