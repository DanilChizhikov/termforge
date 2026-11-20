import {
  dockPreviewRect, resolveDockTarget, WORKSPACE_ROOT, type DockPoint, type DockSlotGeometry, type DockTarget,
} from '../../state/dockTarget';
import type { FloatingRect } from '../../state/floatingGeometry';

export interface DockPreview {
  target: DockTarget;
  rect: FloatingRect;
}

function rectOf(element: HTMLElement): FloatingRect {
  const { x, y, width, height } = element.getBoundingClientRect();
  return { x, y, width, height };
}

function viewportRect(canvas: HTMLElement): FloatingRect {
  const bounds = rectOf(canvas);
  return {
    x: bounds.x + canvas.clientLeft,
    y: bounds.y + canvas.clientTop,
    width: canvas.clientWidth || bounds.width,
    height: canvas.clientHeight || bounds.height,
  };
}

export function getVisibleCanvasRect(canvas: HTMLElement): FloatingRect | undefined {
  const { width, height } = viewportRect(canvas);
  if (width <= 0 || height <= 0) return undefined;
  return { x: canvas.scrollLeft, y: canvas.scrollTop, width, height };
}

export function toCanvasPoint(canvas: HTMLElement, point: DockPoint): DockPoint {
  const { x, y } = viewportRect(canvas);
  return { x: point.x - x + canvas.scrollLeft, y: point.y - y + canvas.scrollTop };
}

function isVisibleTile(element: HTMLElement): boolean {
  return element.closest('[hidden], [data-floating-panel], [data-dock-collapsed]') === null
    && (element.checkVisibility?.() ?? true);
}

function collectSlots(canvas: HTMLDivElement, draggedId: string): DockSlotGeometry[] {
  const headers = new Map<string, FloatingRect>();
  for (const tab of canvas.querySelectorAll<HTMLElement>('[data-dock-tab]')) {
    if (isVisibleTile(tab) && tab.dataset.dockTab !== undefined) {
      headers.set(tab.dataset.dockTab, rectOf(tab));
    }
  }

  const slots: DockSlotGeometry[] = [];
  for (const tile of canvas.querySelectorAll<HTMLElement>('[data-dock-anchor]')) {
    const { dockAnchor, dockActive, dockNative, dockPanels } = tile.dataset;
    if (dockAnchor === undefined || !isVisibleTile(tile) || (dockActive === draggedId && dockPanels === '1')) continue;
    const strip = tile.querySelector<HTMLElement>(':scope > [data-dock-strip]');
    const tab = dockNative === undefined ? undefined : headers.get(dockNative);
    const areas = strip === null ? [] : [rectOf(strip)];
    if (tab !== undefined) areas.push(tab);
    const rect = rectOf(tile);
    if (rect.width > 0 && rect.height > 0) slots.push({ id: dockAnchor, rect, headers: areas });
  }
  return slots;
}

export function getDockPreview(canvas: HTMLDivElement, pointer: DockPoint, draggedId: string): DockPreview | null {
  const viewport = viewportRect(canvas);
  const floating = canvas.ownerDocument.elementsFromPoint?.(pointer.x, pointer.y)
    .map((element) => element.closest<HTMLElement>('[data-floating-panel]'))
    .find((element) => element !== null && element.dataset.floatingPanel !== draggedId);
  const slots = floating === undefined ? collectSlots(canvas, draggedId) : [];
  const target = resolveDockTarget({ pointer, viewport, slots });
  if (target === null) return null;
  const id = target.kind === 'tab' ? target.slotId : target.anchorId;
  const area = id === WORKSPACE_ROOT ? viewport : slots.find((slot) => slot.id === id)?.rect;
  if (area === undefined) return null;
  const relativeArea = { ...area, ...toCanvasPoint(canvas, area) };
  return { target, rect: dockPreviewRect(target, relativeArea) };
}

export function initialDragRect(canvas: HTMLDivElement, element: HTMLElement | undefined, point: DockPoint): FloatingRect {
  const position = toCanvasPoint(canvas, point);
  const source = element?.closest<HTMLElement>('[data-dock-anchor]');
  const area = source === null || source === undefined ? undefined : rectOf(source);
  const width = area?.width ? Math.min(640, Math.max(240, area.width)) : 480;
  const height = area?.height ? Math.min(480, Math.max(144, area.height)) : 320;
  return { x: position.x - width / 2, y: position.y - 18, width, height };
}
