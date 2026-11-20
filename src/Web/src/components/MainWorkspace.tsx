import { Group, Panel, Separator } from 'react-resizable-panels';
import { usePersistentLayout } from '../state/workspaceLayout';
import { CollapsibleRegion } from './CollapsibleRegion';
import { TerminalWorkspace } from './TerminalWorkspace';
import { DualFileBrowser } from './DualFileBrowser';
import { TransferPanel } from './TransferPanel';
import { ArrowUpIcon } from './icons/Icons';
import { MovablePanel } from './workspace/MovablePanel';

export function MainWorkspace() {
  const mainLayout = usePersistentLayout('main-workspace');
  return (
    <div className="main-workspace">
      <Group orientation="vertical" id="main-workspace" className="group" {...mainLayout}>
        <CollapsibleRegion id="terminals" title="Terminals" defaultSize="55%" minSize="15%" keepMounted>
          <TerminalWorkspace />
        </CollapsibleRegion>
        <Separator className="separator separator-horizontal" aria-label="Resize panels" />
        <Panel id="files" defaultSize="45%" minSize="15%">
          <section aria-label="Files" className="region-content">
            <DualFileBrowser />
          </section>
        </Panel>
      </Group>
      <MovablePanel id="transfers" title="Transfers" Icon={ArrowUpIcon}>
        <TransferPanel />
      </MovablePanel>
    </div>
  );
}
