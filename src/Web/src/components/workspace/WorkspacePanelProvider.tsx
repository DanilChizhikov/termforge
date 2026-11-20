import { createContext, useCallback, useContext, useState, type ReactNode } from 'react';
import { normalizeFloatingRect, type FloatingRect } from '../../state/floatingGeometry';

export type PanelPlacement =
  | { mode: 'tiled'; slotId: string }
  | { mode: 'floating'; rect: FloatingRect; returnSlotId: string };

interface WorkspacePanels {
  placements: ReadonlyMap<string, PanelPlacement>;
  slots: ReadonlyMap<string, HTMLElement>;
  floatingLayer: HTMLDivElement | null;
  setFloatingLayer: (layer: HTMLDivElement | null) => void;
  registerPanel: (id: string, slotId: string) => () => void;
  registerSlot: (id: string, element: HTMLElement) => () => void;
  detachPanel: (id: string, rect: FloatingRect) => void;
  dockPanel: (id: string, slotId?: string) => void;
  commitFloatingRect: (id: string, rect: FloatingRect) => void;
}

const WorkspacePanelContext = createContext<WorkspacePanels | null>(null);

export function useWorkspacePanels() {
  return useContext(WorkspacePanelContext);
}

export function usePanelPlacement(id: string): PanelPlacement | undefined {
  return useWorkspacePanels()?.placements.get(id);
}

export function WorkspacePanelProvider({ children }: { children: ReactNode }) {
  const [placements, setPlacements] = useState<ReadonlyMap<string, PanelPlacement>>(() => new Map());
  const [slots, setSlots] = useState<ReadonlyMap<string, HTMLElement>>(() => new Map());
  const [floatingLayer, setFloatingLayer] = useState<HTMLDivElement | null>(null);

  const registerPanel = useCallback((id: string, slotId: string) => {
    setPlacements((current) => new Map(current).set(id, { mode: 'tiled', slotId }));
    return () => setPlacements((current) => without(current, id));
  }, []);

  const registerSlot = useCallback((id: string, element: HTMLElement) => {
    setSlots((current) => new Map(current).set(id, element));
    return () => setSlots((current) => current.get(id) === element ? without(current, id) : current);
  }, []);

  const detachPanel = useCallback((id: string, requested: FloatingRect) => {
    const rect = normalizeFloatingRect(requested);
    if (rect === null) return;
    setPlacements((current) => {
      const placement = current.get(id);
      if (placement?.mode !== 'tiled') return current;
      return new Map(current).set(id, { mode: 'floating', rect, returnSlotId: placement.slotId });
    });
  }, []);

  const dockPanel = useCallback((id: string, slotId?: string) => {
    setPlacements((current) => {
      const placement = current.get(id);
      if (placement?.mode !== 'floating' || (slotId !== undefined && !slots.has(slotId))) return current;
      return new Map(current).set(id, { mode: 'tiled', slotId: slotId ?? placement.returnSlotId });
    });
  }, [slots]);

  const commitFloatingRect = useCallback((id: string, requested: FloatingRect) => {
    const rect = normalizeFloatingRect(requested);
    if (rect === null) return;
    setPlacements((current) => {
      const placement = current.get(id);
      if (placement?.mode !== 'floating') return current;
      return new Map(current).set(id, { ...placement, rect });
    });
  }, []);

  return (
    <WorkspacePanelContext.Provider value={{
      placements, slots, floatingLayer, setFloatingLayer, registerPanel, registerSlot,
      detachPanel, dockPanel, commitFloatingRect,
    }}>
      {children}
    </WorkspacePanelContext.Provider>
  );
}

function without<K, V>(current: ReadonlyMap<K, V>, key: K): ReadonlyMap<K, V> {
  const next = new Map(current);
  next.delete(key);
  return next;
}
