import { Group, Separator } from 'react-resizable-panels';
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
        <Separator className="separator separator-horizontal" />
        <CollapsibleRegion id="files" title="Files" defaultSize="45%" minSize="15%">
          <Group orientation="horizontal" id="file-panes" className="group" {...fileLayout}>
            <CollapsibleRegion id="local-files" title="Local" defaultSize="50%" minSize="15%">
              <LocalFilePane />
            </CollapsibleRegion>
            <Separator className="separator separator-vertical" />
            <CollapsibleRegion id="remote-files" title="Remote" defaultSize="50%" minSize="15%">
              <RemoteFilePane />
            </CollapsibleRegion>
          </Group>
        </CollapsibleRegion>
      </Group>
      <TransferPanel />
    </div>
  );
}
