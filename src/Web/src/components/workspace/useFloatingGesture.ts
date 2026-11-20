import { useCallback, useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react';
import { containsPoint, type DockPoint } from '../../state/dockTarget';
import { adjustFloatingRect, type FloatingDirection, type FloatingRect } from '../../state/floatingGeometry';
import { getDockPreview, initialDragRect, toCanvasPoint, type DockPreview } from './workspaceDockGeometry';
import type { useWorkspacePanels } from './WorkspacePanelProvider';

export interface GestureTarget {
  panelId: string;
  rect: FloatingRect;
  direction: FloatingDirection;
  preview: (rect: FloatingRect, dragging: boolean) => void;
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
  preview?: GestureTarget['preview'];
  strip?: FloatingRect;
  active: boolean;
  pointerId: number;
  start: DockPoint;
  startOnCanvas: DockPoint;
  pointer: DockPoint;
}

interface DragPreview {
  dock: DockPreview | null;
  ghost: FloatingRect | null;
}

const EMPTY_PREVIEW: DragPreview = { dock: null, ghost: null };

export function useFloatingGesture(canvas: HTMLDivElement | null, panels: ReturnType<typeof useWorkspacePanels>) {
  const gesture = useRef<Gesture | null>(null);
  const [preview, setPreview] = useState(EMPTY_PREVIEW);
  const dropPanel = panels?.dropPanel;

  const start: StartFloatingGesture = useCallback((event, target) => {
    if (gesture.current !== null || event.button !== 0 || !event.isPrimary || canvas === null) return;
    const point = { x: event.clientX, y: event.clientY };
    const startOnCanvas = toCanvasPoint(canvas, point);
    const pointer = { pointerId: event.pointerId, start: point, startOnCanvas, pointer: point };
    const { panelId } = target;
    if (target.direction === 'detach') {
      const placement = panels?.placements.get(panelId);
      const strip = event.currentTarget.closest<HTMLElement>('[data-dock-strip], [role="tablist"]');
      if (placement?.mode !== 'tiled' || strip === null) return;
      const rect = initialDragRect(canvas, panels?.slots.get(placement.slotId), point);
      gesture.current = {
        panelId, direction: 'move', rect, strip: strip.getBoundingClientRect(), active: false,
        ...pointer,
      };
      return;
    }
    canvas.setPointerCapture(event.pointerId);
    gesture.current = { ...target, active: true, ...pointer };
    target.preview(target.rect, target.direction === 'move');
    event.preventDefault();
  }, [canvas, panels]);

  const update = useCallback((element: HTMLDivElement, current: Gesture, point: DockPoint) => {
    current.pointer = point;
    if (!current.active) {
      if (current.strip === undefined || containsPoint(current.strip, point)
        || Math.hypot(point.x - current.start.x, point.y - current.start.y) < 8) return;
      element.setPointerCapture(current.pointerId);
      current.active = true;
    }
    const position = toCanvasPoint(element, point);
    const rect = rectAt(current, position);
    current.preview?.(rect, current.direction === 'move');
    if (current.direction !== 'move') return;
    const dock = getDockPreview(element, point, current.panelId);
    setPreview({ dock, ghost: current.strip !== undefined && dock === null ? rect : null });
  }, []);

  const finish = useCallback((pointerId: number, point: DockPoint | null) => {
    const current = gesture.current;
    if (current?.pointerId !== pointerId) return;
    gesture.current = null;
    if (!current.active) return;
    current.preview?.(current.rect, false);
    setPreview(EMPTY_PREVIEW);
    if (point !== null && canvas !== null) {
      const target = current.direction === 'move' ? getDockPreview(canvas, point, current.panelId)?.target ?? null : null;
      const position = toCanvasPoint(canvas, point);
      const rect = rectAt(current, position);
      dropPanel?.({ panelId: current.panelId, mode: current.strip === undefined ? 'floating' : 'tiled', rect, target });
    }
    if (canvas?.hasPointerCapture(pointerId)) canvas.releasePointerCapture(pointerId);
  }, [canvas, dropPanel]);

  useEffect(() => {
    const move = (event: PointerEvent) => {
      const current = gesture.current;
      if (current?.pointerId !== event.pointerId || canvas === null) return;
      update(canvas, current, { x: event.clientX, y: event.clientY });
    };
    const scroll = () => {
      const current = gesture.current;
      if (current?.active && canvas !== null) update(canvas, current, current.pointer);
    };
    const up = (event: PointerEvent) => finish(event.pointerId, { x: event.clientX, y: event.clientY });
    const cancel = (event: PointerEvent) => finish(event.pointerId, null);
    const keyDown = (event: KeyboardEvent) => {
      if (event.key !== 'Escape' || gesture.current === null) return;
      finish(gesture.current.pointerId, null);
      event.preventDefault();
      event.stopPropagation();
    };
    canvas?.addEventListener('scroll', scroll);
    window.addEventListener('pointermove', move);
    window.addEventListener('pointerup', up);
    window.addEventListener('pointercancel', cancel);
    window.addEventListener('keydown', keyDown, true);
    return () => {
      canvas?.removeEventListener('scroll', scroll);
      window.removeEventListener('pointermove', move);
      window.removeEventListener('pointerup', up);
      window.removeEventListener('pointercancel', cancel);
      window.removeEventListener('keydown', keyDown, true);
    };
  }, [canvas, finish, update]);

  const lostCapture = (event: ReactPointerEvent<HTMLDivElement>) => {
    if (event.target === event.currentTarget) finish(event.pointerId, null);
  };

  return { start, preview, lostCapture };
}

function rectAt(gesture: Gesture, point: DockPoint): FloatingRect {
  const delta = { x: point.x - gesture.startOnCanvas.x, y: point.y - gesture.startOnCanvas.y };
  return adjustFloatingRect(gesture.rect, gesture.direction, delta);
}
