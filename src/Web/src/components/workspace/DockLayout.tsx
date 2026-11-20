import { useContext, type ReactNode } from 'react';
import { Group, Panel, Separator } from 'react-resizable-panels';
import { WORKSPACE_ROOT } from '../../state/dockTarget';
import { originStack, type DockNode, type DockStack } from '../../state/workspaceDocking';
import { useRegionToggleState } from '../CollapsibleRegion';
import { PanelSlot } from './PanelSlot';
import { FloatingGestureContext } from './WorkspaceCanvas';
import { useWorkspacePanels } from './WorkspacePanelProvider';

interface DockOrigin {
  slotId: string;
  panelId?: string;
  root?: ReactNode;
}

export function DockLayout({ origin, hidden }: { origin: DockOrigin; hidden?: boolean }) {
  const panels = useWorkspacePanels();
  const region = useRegionToggleState();
  const tree = panels?.trees.get(origin.slotId) ?? originStack(origin.slotId);
  const native = origin.panelId === undefined ? undefined : panels?.placements.get(origin.panelId);
  const hasNative = native?.mode === 'tiled' && native.slotId === origin.slotId;
  const visible = visibleTree(tree, hasNative || origin.slotId === WORKSPACE_ROOT);
  if (visible === null) return null;
  return (
    <div className="workspace-dock-layout" hidden={hidden} data-dock-collapsed={region?.collapsed || undefined}>
      <DockTile node={visible} origin={origin} />
    </div>
  );
}

function visibleTree(node: DockNode, hasNative: boolean): DockNode | null {
  if (node.kind === 'stack') return node.tabs.length > 0 || (node.origin && hasNative) ? node : null;
  const first = visibleTree(node.first, hasNative);
  const second = visibleTree(node.second, hasNative);
  if (first === null) return second;
  if (second === null) return first;
  return { ...node, first, second };
}

function DockTile({ node, origin }: { node: DockNode; origin: DockOrigin }) {
  const panels = useWorkspacePanels();
  if (node.kind === 'stack') {
    return node.id === WORKSPACE_ROOT ? origin.root : <TileStack stack={node} origin={origin} />;
  }

  const firstId = `${node.id}-first`;
  const secondId = `${node.id}-second`;
  const separator = node.orientation === 'horizontal' ? 'separator-vertical' : 'separator-horizontal';
  return (
    <Group
      key={node.id}
      id={node.id}
      orientation={node.orientation}
      defaultLayout={{ [firstId]: node.firstSize, [secondId]: 100 - node.firstSize }}
      onLayoutChanged={(layout, meta) => {
        if (meta.isUserInteraction && layout[firstId] !== undefined) panels?.resizeSplit(node.id, layout[firstId]);
      }}
      className="group workspace-dock-split"
    >
      <Panel id={firstId} minSize="10%">
        <DockTile node={node.first} origin={origin} />
      </Panel>
      <Separator className={`separator ${separator}`} aria-label="Resize docked panels" />
      <Panel id={secondId} minSize="10%">
        <DockTile node={node.second} origin={origin} />
      </Panel>
    </Group>
  );
}

function TileStack({ stack, origin }: { stack: DockStack; origin: DockOrigin }) {
  const panels = useWorkspacePanels();
  const gesture = useContext(FloatingGestureContext);
  const native = origin.panelId === undefined ? undefined : panels?.placements.get(origin.panelId);
  const allTabs = stack.origin && origin.panelId !== undefined && native?.mode === 'tiled' && native.slotId === origin.slotId
    ? [{ panelId: origin.panelId, slotId: origin.slotId }, ...stack.tabs] : stack.tabs;
  const selected = allTabs.find((item) => item.panelId === stack.selected) ?? allTabs[0];
  if (selected === undefined || panels === null) return null;
  const title = panels.titles.get(selected.panelId) ?? selected.panelId;
  const tabbed = allTabs.length > 1;
  return (
    <section
      className="workspace-dock-tile"
      data-dock-anchor={stack.id}
      data-dock-native={stack.origin ? origin.panelId : undefined}
      data-dock-active={selected.panelId}
      data-dock-panels={allTabs.length}
    >
      <header className="workspace-dock-header" data-dock-strip>
        {tabbed ? (
          <div className="workspace-dock-tabs" role="tablist" aria-label="Docked panels">
            {allTabs.map(({ panelId, slotId }) => (
              <button
                key={panelId}
                id={`dock-tab-${slotId}`}
                type="button"
                className="workspace-tab"
                role="tab"
                aria-selected={selected.panelId === panelId}
                aria-controls={`dock-body-${slotId}`}
                onClick={() => panels.selectTab(stack.id, panelId)}
                onPointerDown={(event) => gesture?.(event, { panelId, direction: 'detach' })}
              >
                {panels.titles.get(panelId) ?? panelId}
              </button>
            ))}
          </div>
        ) : stack.origin ? null : <span className="workspace-dock-title">{title}</span>}
        <button
          type="button"
          className="btn btn-sm"
          aria-label={`Detach ${title}`}
          onClick={(event) => {
            const tile = event.currentTarget.closest<HTMLElement>('[data-dock-anchor]');
            const canvas = tile?.closest<HTMLElement>('.workspace-canvas');
            if (tile === null || tile === undefined || canvas === null || canvas === undefined) return;
            const place = tile.getBoundingClientRect();
            const bounds = canvas.getBoundingClientRect();
            panels.detachPanel(selected.panelId, {
              x: place.width > 0 ? place.x - bounds.x + 16 : 16,
              y: place.height > 0 ? place.y - bounds.y + 16 : 16,
              width: place.width > 0 ? Math.min(640, place.width) : 480,
              height: place.height > 0 ? Math.min(480, place.height) : 320,
            });
          }}
        >
          Detach
        </button>
      </header>
      {allTabs.map(({ panelId, slotId }) => (
        <div
          key={slotId}
          id={tabbed ? `dock-body-${slotId}` : undefined}
          role={tabbed ? 'tabpanel' : undefined}
          aria-labelledby={tabbed ? `dock-tab-${slotId}` : undefined}
          className="workspace-dock-body"
          hidden={selected.panelId !== panelId}
        >
          <PanelSlot id={slotId} panelId={panelId} />
        </div>
      ))}
    </section>
  );
}
