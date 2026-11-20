import { useCallback, useEffect, useRef, useState, type PointerEvent as ReactPointerEvent, type RefObject } from 'react';
import { containsPoint, type DockPoint } from '../../state/dockTarget';
import { adjustFloatingRect, type FloatingDirection, type FloatingRect } from '../../state/floatingGeometry';
import { getDockPreview, initialDragRect, type DockPreview } from './workspaceDockGeometry';
import type { useWorkspacePanels } from './WorkspacePanelProvider';

export interface GestureTarget {
  panelId: string;
  rect: FloatingRect;
  direction: FloatingDirection;
  preview: (rect: FloatingRect) => void;
}

interface DetachTarget {
  panelId: string;
  direction: 'detach';
}

export type StartFloatingGesture = (event: ReactPointerEvent<HTMLElement>, target: GestureTarget | DetachTarget) => void;

interface Gesture {
  panelId: string;
  rect: FloatingRect;
  direction: FloatingDirection;
  preview?: (rect: FloatingRect) => void;
  strip?: FloatingRect;
  active: boolean;
  pointerId: number;
  start: DockPoint;
}

interface DragPreview {
  dock: DockPreview | null;
  ghost: FloatingRect | null;
}

const EMPTY_PREVIEW: DragPreview = { dock: null, ghost: null };

export function useFloatingGesture(canvas: RefObject<HTMLDivElement | null>, panels: ReturnType<typeof useWorkspacePanels>) {
  const gesture = useRef<Gesture | null>(null);
  const [preview, setPreview] = useState(EMPTY_PREVIEW);

  const start: StartFloatingGesture = useCallback((event, target) => {
    const element = canvas.current;
    if (gesture.current !== null || event.button !== 0 || !event.isPrimary || element === null) return;
    const point = { x: event.clientX, y: event.clientY };
    const { panelId } = target;
    if (target.direction === 'detach') {
      const placement = panels?.placements.get(panelId);
      const strip = event.currentTarget.closest<HTMLElement>('[data-dock-strip], [role="tablist"]');
      if (placement?.mode !== 'tiled' || strip === null) return;
      const rect = initialDragRect(element, panels?.slots.get(placement.slotId), point);
      gesture.current = {
        panelId, direction: 'move', rect, strip: strip.getBoundingClientRect(), active: false,
        pointerId: event.pointerId, start: point,
      };
      return;
    }
    element.setPointerCapture(event.pointerId);
    gesture.current = { ...target, active: true, pointerId: event.pointerId, start: point };
    event.preventDefault();
  }, [canvas, panels]);

  const move = useCallback((event: PointerEvent) => {
    const current = gesture.current;
    const element = canvas.current;
    if (current?.pointerId !== event.pointerId || element === null) return;
    const point = { x: event.clientX, y: event.clientY };
    if (!current.active) {
      if (current.strip === undefined || containsPoint(current.strip, point)
        || Math.hypot(point.x - current.start.x, point.y - current.start.y) < 8) return;
      element.setPointerCapture(event.pointerId);
      current.active = true;
    }
    const rect = rectAt(current, point);
    current.preview?.(rect);
    if (current.direction !== 'move') return;
    const dock = getDockPreview(element, point, current.panelId);
    setPreview({ dock, ghost: current.strip !== undefined && dock === null ? rect : null });
  }, [canvas]);

  const finish = useCallback((pointerId: number, point: DockPoint | null) => {
    const current = gesture.current;
    if (current?.pointerId !== pointerId) return;
    gesture.current = null;
    if (!current.active) return;
    const element = canvas.current;
    const rect = point === null ? current.rect : rectAt(current, point);
    current.preview?.(rect);
    setPreview(EMPTY_PREVIEW);
    if (point !== null && element !== null) {
      const target = current.direction === 'move' ? getDockPreview(element, point, current.panelId)?.target ?? null : null;
      panels?.dropPanel({ panelId: current.panelId, mode: current.strip === undefined ? 'floating' : 'tiled', rect, target });
    }
    if (element?.hasPointerCapture(pointerId)) element.releasePointerCapture(pointerId);
  }, [canvas, panels]);

  useEffect(() => {
    const up = (event: PointerEvent) => finish(event.pointerId, { x: event.clientX, y: event.clientY });
    const cancel = (event: PointerEvent) => finish(event.pointerId, null);
    const keyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape' || gesture.current === null) return;
      finish(gesture.current.pointerId, null);
      event.preventDefault();
      event.stopPropagation();
    };
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
    window.addEventListener('pointercancel', cancel);
    window.addEventListener('keydown', keyDown, true);
    return () => {
      window.removeEventListener('pointermove', move);
      window.removeEventListener('pointerup', up);
      window.removeEventListener('pointercancel', cancel);
      window.removeEventListener('keydown', keyDown, true);
    };
  }, [finish, move]);

  const lostCapture = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (event.target === event.currentTarget) finish(event.pointerId, null);
  };

  return { start, preview, lostCapture };
}

function rectAt(gesture: Gesture, point: DockPoint): FloatingRect {
  const delta = { x: point.x - gesture.start.x, y: point.y - gesture.start.y };
  return adjustFloatingRect(gesture.rect, gesture.direction, delta);
}
