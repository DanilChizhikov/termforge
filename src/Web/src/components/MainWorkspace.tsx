import { Group, Panel, Separator } from 'react-resizable-panels';
import { usePersistentLayout } from '../state/workspaceLayout';
import { CollapsibleRegion } from './CollapsibleRegion';
import { TerminalWorkspace } from './TerminalWorkspace';
import { LocalFilePane } from './LocalFilePane';
import { RemoteFilePane } from './RemoteFilePane';
import { TransferPanel } from './TransferPanel';

export function MainWorkspace() {
  const mainLayout = usePersistentLayout('main-workspace');
  const fileLayout = usePersistentLayout('file-panes');
  return (
    <div className="main-workspace">
      <Group orientation="vertical" id="main-workspace" className="group" {...mainLayout}>
        <CollapsibleRegion id="terminals" title="Terminals" defaultSize="55%" minSize="15%" keepMounted>
          <TerminalWorkspace />
        </CollapsibleRegion>
        <Separator className="separator separator-horizontal" aria-label="Resize panels" />
        <Panel id="files" defaultSize="45%" minSize="15%">
          <section aria-label="Files" className="region-content">
            <Group orientation="horizontal" id="file-panes" className="group" {...fileLayout}>
              <CollapsibleRegion id="local-files" title="Local" defaultSize="50%" minSize="15%" collapsedLayout="rail">
                <LocalFilePane />
              </CollapsibleRegion>
              <Separator className="separator separator-vertical" aria-label="Resize panels" />
              <CollapsibleRegion id="remote-files" title="Remote" defaultSize="50%" minSize="15%" collapsedLayout="rail">
                <RemoteFilePane />
              </CollapsibleRegion>
            </Group>
          </section>
        </Panel>
      </Group>
      <TransferPanel />
    </div>
  );
}
