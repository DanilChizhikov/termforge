import { useCallback } from 'react';
import { useWorkspacePanels } from './WorkspacePanelProvider';

export function PanelSlot({ id, panelId }: { id: string; panelId?: string }) {
  const registerSlot = useWorkspacePanels()?.registerSlot;
  const slotRef = useCallback((element: HTMLDivElement | null) => {
    if (element !== null) return registerSlot?.(id, element);
  }, [id, registerSlot]);

  return <div ref={slotRef} className="workspace-panel-slot" data-workspace-slot={id} data-workspace-panel={panelId} />;
}
