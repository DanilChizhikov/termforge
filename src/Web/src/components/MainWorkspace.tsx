import { Group, Separator } from 'react-resizable-panels';
import { CollapsibleRegion } from './CollapsibleRegion';
import { EmptyState } from './EmptyState';

export function MainWorkspace() {
  return (
    <Group orientation="vertical" id="main-workspace" className="group">
      <CollapsibleRegion id="terminals" title="Terminals" defaultSize="55%" minSize="15%">
        <div aria-label="Terminal tabs" className="tab-strip" />
        <EmptyState message="No terminal sessions. Connect to a server to open one." />
      </CollapsibleRegion>
      <Separator className="separator separator-horizontal" />
      <CollapsibleRegion id="files" title="Files" defaultSize="45%" minSize="15%">
        <Group orientation="horizontal" id="file-panes" className="group">
          <CollapsibleRegion id="local-files" title="Local" defaultSize="50%" minSize="15%">
            <EmptyState message="Local file browser is not available yet." />
          </CollapsibleRegion>
          <Separator className="separator separator-vertical" />
          <CollapsibleRegion id="remote-files" title="Remote" defaultSize="50%" minSize="15%">
            <EmptyState message="Connect to a server to browse remote files." />
          </CollapsibleRegion>
        </Group>
      </CollapsibleRegion>
    </Group>
  );
}
