import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import type { DockTarget } from '../../state/dockTarget';
import type { FloatingRect } from '../../state/floatingGeometry';
import {
  applyPanelDrop, emptyDockLayout, raiseFloatingPanel, registerDockPanel, resizeDockSplit, returnDockPanel, selectDockTab, unregisterDockPanel,
  type DockNode, type PanelPlacement,
} from '../../state/workspaceDocking';
import { getVisibleCanvasRect } from './workspaceDockGeometry';

export type { PanelPlacement } from '../../state/workspaceDocking';

interface WorkspacePanels {
  placements: ReadonlyMap<string, PanelPlacement>;
  trees: ReadonlyMap<string, DockNode>;
  titles: ReadonlyMap<string, string>;
  slots: ReadonlyMap<string, HTMLElement>;
  floatingLayer: HTMLDivElement | null;
  floatingCanvas: ReturnType<typeof getFloatingCanvas>;
  setFloatingLayer: (layer: HTMLDivElement | null) => void;
  registerPanel: (id: string, slotId: string) => () => void;
  updateTitle: (id: string, title: string) => void;
  registerSlot: (id: string, element: HTMLElement) => () => void;
  detachPanel: (id: string, rect: FloatingRect) => void;
  dockPanel: (id: string, slotId?: string) => void;
  commitFloatingRect: (id: string, rect: FloatingRect) => void;
  dropPanel: (drop: { panelId: string; mode: PanelPlacement['mode']; rect: FloatingRect; target: DockTarget | null }) => void;
  raisePanel: (id: string) => void;
  selectTab: (stackId: string, panelId: string) => void;
  resizeSplit: (id: string, firstSize: number) => void;
}

const WorkspacePanelContext = createContext<WorkspacePanels | null>(null);

export function useWorkspacePanels() {
  return useContext(WorkspacePanelContext);
}

export function usePanelPlacement(id: string): PanelPlacement | undefined {
  return useWorkspacePanels()?.placements.get(id);
}

export function isPanelAway(placement: PanelPlacement | undefined, originalSlotId: string): boolean {
  return placement !== undefined && (placement.mode === 'floating' || placement.slotId !== originalSlotId);
}

export function WorkspacePanelProvider({ children }: { children: ReactNode }) {
  const [layout, setLayout] = useState(emptyDockLayout);
  const [titles, setTitles] = useState<ReadonlyMap<string, string>>(() => new Map());
  const [slots, setSlots] = useState<ReadonlyMap<string, HTMLElement>>(() => new Map());
  const [floatingLayer, setFloatingLayer] = useState<HTMLDivElement | null>(null);
  const floatingCanvas = useMemo(() => getFloatingCanvas(layout.placements), [layout.placements]);

  const registerPanel = useCallback((id: string, slotId: string) => {
    setLayout((current) => registerDockPanel(current, id, slotId));
    return () => {
      setLayout((current) => unregisterDockPanel(current, id));
      setTitles((current) => without(current, id));
    };
  }, []);

  const updateTitle = useCallback((id: string, title: string) => {
    setTitles((current) => new Map(current).set(id, title));
  }, []);

  const registerSlot = useCallback((id: string, element: HTMLElement) => {
    setSlots((current) => new Map(current).set(id, element));
    return () => setSlots((current) => current.get(id) === element ? without(current, id) : current);
  }, []);

  const dropPanel: WorkspacePanels['dropPanel'] = useCallback((drop) => {
    const canvas = floatingLayer?.closest<HTMLElement>('.workspace-canvas');
    const viewport = canvas == null || drop.target !== null ? undefined : getVisibleCanvasRect(canvas);
    setLayout((current) => applyPanelDrop(current, drop, viewport));
  }, [floatingLayer]);

  const raisePanel = useCallback((id: string) => {
    setLayout((current) => raiseFloatingPanel(current, id));
  }, []);

  const detachPanel = useCallback((id: string, rect: FloatingRect) => {
    dropPanel({ panelId: id, mode: 'tiled', rect, target: null });
  }, [dropPanel]);

  const dockPanel = useCallback((id: string, slotId?: string) => {
    setLayout((current) => {
      if (slotId === undefined) return returnDockPanel(current, id, slots);
      if (!slots.has(slotId) || current.placements.get(id)?.mode !== 'floating') return current;
      return { ...current, placements: new Map(current.placements).set(id, { mode: 'tiled', slotId }) };
    });
  }, [slots]);

  const commitFloatingRect = useCallback((id: string, rect: FloatingRect) => {
    dropPanel({ panelId: id, mode: 'floating', rect, target: null });
  }, [dropPanel]);

  const selectTab = useCallback((stackId: string, panelId: string) => {
    setLayout((current) => selectDockTab(current, stackId, panelId));
  }, []);

  const resizeSplit = useCallback((id: string, firstSize: number) => {
    setLayout((current) => resizeDockSplit(current, id, firstSize));
  }, []);

  return (
    <WorkspacePanelContext.Provider value={{
      placements: layout.placements, trees: layout.trees, titles, slots, floatingLayer, floatingCanvas, setFloatingLayer,
      registerPanel, updateTitle, registerSlot, detachPanel, dockPanel, commitFloatingRect, dropPanel, raisePanel, selectTab, resizeSplit,
    }}>
      {children}
    </WorkspacePanelContext.Provider>
  );
}

function getFloatingCanvas(placements: ReadonlyMap<string, PanelPlacement>) {
  let width = 0;
  let height = 0;
  const zIndices = new Map<string, number>();
  for (const [id, placement] of placements) {
    if (placement.mode !== 'floating') continue;
    const { x, y, width: windowWidth, height: windowHeight } = placement.rect;
    width = Math.max(width, x + windowWidth);
    height = Math.max(height, y + windowHeight);
    zIndices.set(id, zIndices.size + 1);
  }
  return { width, height, zIndices };
}

function without<K, V>(current: ReadonlyMap<K, V>, key: K): ReadonlyMap<K, V> {
  const next = new Map(current);
  next.delete(key);
  return next;
}
