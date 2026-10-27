import { useCallback, useState } from 'react';
import type { Layout, LayoutChangedMeta } from 'react-resizable-panels';
import type { WorkspacePanelLayout } from '../api/workspace';

export interface WorkspaceLayoutSnapshot {
  layouts: ReadonlyMap<string, Layout>;
  collapsed: ReadonlySet<string>;
}

export interface PersistentLayout {
  defaultLayout: Layout | undefined;
  onLayoutChanged: (layout: Layout, meta: LayoutChangedMeta) => void;
}

const EMPTY: WorkspaceLayoutSnapshot = { layouts: new Map(), collapsed: new Set() };

let snapshot: WorkspaceLayoutSnapshot = EMPTY;
let listeners: Array<() => void> = [];

function update(next: WorkspaceLayoutSnapshot): void {
  snapshot = next;
  listeners.forEach((listener) => listener());
}

function roundSizes(layout: Layout): Layout {
  const rounded: Layout = {};
  for (const [id, size] of Object.entries(layout)) {
    rounded[id] = Math.round(size * 100) / 100;
  }
  return rounded;
}

export function subscribeWorkspaceLayout(listener: () => void): () => void {
  listeners = [...listeners, listener];
  return () => {
    listeners = listeners.filter((item) => item !== listener);
  };
}

export function getWorkspaceLayout(): WorkspaceLayoutSnapshot {
  return snapshot;
}

export function getPanelLayout(groupId: string): Layout | undefined {
  return snapshot.layouts.get(groupId);
}

export function recordPanelLayout(groupId: string, layout: Layout): void {
  const layouts = new Map(snapshot.layouts);
  layouts.set(groupId, roundSizes(layout));
  update({ ...snapshot, layouts });
}

export function isPanelCollapsed(panelId: string): boolean {
  return snapshot.collapsed.has(panelId);
}

export function recordPanelCollapsed(panelId: string, collapsed: boolean): void {
  if (snapshot.collapsed.has(panelId) === collapsed) {
    return;
  }
  const next = new Set(snapshot.collapsed);
  if (collapsed) {
    next.add(panelId);
  } else {
    next.delete(panelId);
  }
  update({ ...snapshot, collapsed: next });
}

export function restoreWorkspaceLayout(layouts: readonly WorkspacePanelLayout[], collapsed: readonly string[]): void {
  const restored = new Map<string, Layout>();
  for (const layout of layouts) {
    restored.set(layout.groupId, { ...layout.sizes });
  }
  update({ layouts: restored, collapsed: new Set(collapsed) });
}

export function resetWorkspaceLayout(): void {
  update(EMPTY);
}

export function usePersistentLayout(groupId: string): PersistentLayout {
  const [defaultLayout] = useState(() => getPanelLayout(groupId));
  const onLayoutChanged = useCallback(
    (layout: Layout, meta: LayoutChangedMeta) => {
      if (meta.isUserInteraction) {
        recordPanelLayout(groupId, meta.requestedLayout ?? layout);
      }
    },
    [groupId],
  );
  return { defaultLayout, onLayoutChanged };
}
