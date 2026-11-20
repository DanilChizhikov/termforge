import { useContext, type PointerEvent } from 'react';
import { FloatingGestureContext } from './WorkspaceCanvas';
import { useWorkspacePanels } from './WorkspacePanelProvider';

export function useDockTab() {
  const start = useContext(FloatingGestureContext);
  const panels = useWorkspacePanels();
  return (panelId: string) => {
    const placement = panels?.placements.get(panelId);
    if (start === null || placement?.mode !== 'tiled' || placement.slotId !== panelId) return {};
    return {
      'data-dock-tab': panelId,
      onPointerDown: (event: PointerEvent<HTMLButtonElement>) => start(event, { panelId, direction: 'detach' }),
    };
  };
}
