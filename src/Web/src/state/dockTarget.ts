import type { FloatingRect } from './floatingGeometry';

export const WORKSPACE_ROOT = 'workspace-root';
const SIDES = ['left', 'right', 'top', 'bottom'] as const;
export type DockSide = (typeof SIDES)[number];
export type DockTarget = { kind: 'tab'; slotId: string } | { kind: 'split'; anchorId: string; side: DockSide };
export type DockPoint = { x: number; y: number };

export interface DockSlotGeometry {
  id: string;
  rect: FloatingRect;
  headers: readonly FloatingRect[];
}

interface DockGeometry {
  pointer: DockPoint;
  viewport: FloatingRect;
  slots: readonly DockSlotGeometry[];
}

interface Edge {
  side: DockSide;
  distance: number;
}

export function containsPoint(rect: FloatingRect, point: DockPoint): boolean {
  return rect.width > 0 && rect.height > 0 && point.x >= rect.x && point.x <= rect.x + rect.width
    && point.y >= rect.y && point.y <= rect.y + rect.height;
}

function nearestEdge(rect: FloatingRect, pointer: DockPoint, limit: number): Edge | undefined {
  if (!containsPoint(rect, pointer)) return undefined;
  const distances = [pointer.x - rect.x, rect.x + rect.width - pointer.x, pointer.y - rect.y, rect.y + rect.height - pointer.y];
  let result: Edge | undefined;
  for (const [index, side] of SIDES.entries()) {
    const distance = distances[index]!;
    if (distance <= limit && (result === undefined || distance < result.distance)) result = { side, distance };
  }
  return result;
}

export function resolveDockTarget({ pointer, viewport, slots }: DockGeometry): DockTarget | null {
  if (!containsPoint(viewport, pointer)) return null;
  const rootEdge = nearestEdge(viewport, pointer, 24);
  if (rootEdge !== undefined) return { kind: 'split', anchorId: WORKSPACE_ROOT, side: rootEdge.side };

  let paneEdge: (Edge & { id: string }) | undefined;
  for (const slot of slots) {
    const edge = nearestEdge(slot.rect, pointer, 12);
    if (edge !== undefined && (paneEdge === undefined || edge.distance < paneEdge.distance)) {
      paneEdge = { ...edge, id: slot.id };
    }
  }
  if (paneEdge !== undefined) return { kind: 'split', anchorId: paneEdge.id, side: paneEdge.side };

  const header = slots.find((slot) => slot.headers.some((rect) => containsPoint(rect, pointer)));
  return header === undefined ? null : { kind: 'tab', slotId: header.id };
}

export function dockPreviewRect(target: DockTarget, area: FloatingRect): FloatingRect {
  if (target.kind === 'tab') return area;
  const fraction = target.anchorId === WORKSPACE_ROOT ? 0.3 : 0.5;
  if (target.side === 'left' || target.side === 'right') {
    const width = area.width * fraction;
    const x = target.side === 'left' ? area.x : area.x + area.width - width;
    return { ...area, x, width };
  }
  const height = area.height * fraction;
  const y = target.side === 'top' ? area.y : area.y + area.height - height;
  return { ...area, y, height };
}
