import { Group, Separator } from 'react-resizable-panels';
import { CollapsibleRegion } from './components/CollapsibleRegion';
import { EmptyState } from './components/EmptyState';
import { SessionBanner } from './components/SessionBanner';
import { WorkspaceTabs } from './components/WorkspaceTabs';

export function App() {
  return (
    <div className="app">
      <SessionBanner />
      <Group orientation="horizontal" id="app-layout" className="group">
        <CollapsibleRegion id="servers" title="Servers" defaultSize="20%" minSize="10%">
          <EmptyState message="No servers yet." />
        </CollapsibleRegion>
        <Separator className="separator separator-vertical" />
        <CollapsibleRegion id="main" title="Workspace" defaultSize="80%" minSize="30%">
          <WorkspaceTabs />
        </CollapsibleRegion>
      </Group>
    </div>
  );
}
