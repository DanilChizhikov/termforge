import { useRef, type PointerEvent, type RefObject } from 'react';
import { adjustFloatingRect, type FloatingDirection, type FloatingRect } from '../../state/floatingGeometry';

export interface GestureTarget {
  panelId: string;
  rect: FloatingRect;
  direction: FloatingDirection;
  preview: (rect: FloatingRect) => void;
}

export type StartFloatingGesture = (event: PointerEvent, target: GestureTarget) => void;

interface Gesture extends GestureTarget {
  pointerId: number;
  x: number;
  y: number;
}

export function useFloatingGesture(canvas: RefObject<HTMLDivElement | null>, commit: (id: string, rect: FloatingRect) => void) {
  const gesture = useRef<Gesture | null>(null);

  const start: StartFloatingGesture = (event, target) => {
    if (gesture.current !== null || event.button !== 0 || !event.isPrimary || canvas.current === null) return;
    canvas.current.setPointerCapture(event.pointerId);
    gesture.current = { ...target, pointerId: event.pointerId, x: event.clientX, y: event.clientY };
    event.preventDefault();
  };

  const move = (event: PointerEvent) => {
    const current = gesture.current;
    if (current?.pointerId === event.pointerId) current.preview(rectAt(current, event));
  };

  const finish = (event: PointerEvent, shouldCommit: boolean) => {
    const current = gesture.current;
    if (current?.pointerId !== event.pointerId) return;
    gesture.current = null;
    const rect = shouldCommit ? rectAt(current, event) : current.rect;
    current.preview(rect);
    if (shouldCommit) commit(current.panelId, rect);
    if (canvas.current?.hasPointerCapture(event.pointerId)) canvas.current.releasePointerCapture(event.pointerId);
  };

  return { start, move, finish };
}

function rectAt(gesture: Gesture, event: PointerEvent): FloatingRect {
  const delta = { x: event.clientX - gesture.x, y: event.clientY - gesture.y };
  return adjustFloatingRect(gesture.rect, gesture.direction, delta);
}
