import { Group, Separator } from 'react-resizable-panels';
import { CollapsibleRegion } from './components/CollapsibleRegion';
import { HostKeyPrompt } from './components/HostKeyPrompt';
import { ServerSidebar } from './components/ServerSidebar';
import { SessionBanner } from './components/SessionBanner';
import { UpdateNotice } from './components/UpdateNotice';
import { WorkspaceTabs } from './components/WorkspaceTabs';

export function App() {
  return (
    <div className="app">
      <SessionBanner />
      <UpdateNotice />
      <Group orientation="horizontal" id="app-layout" className="group">
        <CollapsibleRegion id="servers" title="Servers" defaultSize="20%" minSize="10%">
          <ServerSidebar />
        </CollapsibleRegion>
        <Separator className="separator separator-vertical" />
        <CollapsibleRegion id="main" title="Workspace" defaultSize="80%" minSize="30%">
          <WorkspaceTabs />
        </CollapsibleRegion>
      </Group>
      <HostKeyPrompt />
    </div>
  );
}
