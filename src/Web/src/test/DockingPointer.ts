import { fireEvent } from '@testing-library/react';
import { vi } from 'vitest';
import type { FloatingRect } from '../state/floatingGeometry';

export const CANVAS_RECT = { x: 50, y: 40, width: 1000, height: 800 };
const POINTER = { pointerId: 7, isPrimary: true, button: 0 };

export function measure(element: HTMLElement, rect: FloatingRect) {
  Object.defineProperty(element, 'getBoundingClientRect', {
    configurable: true,
    value: () => new DOMRect(rect.x, rect.y, rect.width, rect.height),
  });
}

export function dockPointer(container: HTMLElement) {
  const canvas = container.querySelector<HTMLElement>('.workspace-canvas')!;
  measure(canvas, CANVAS_RECT);
  let captured: number | null = null;
  const capture = vi.fn((id: number) => { captured = id; });
  const release = vi.fn(() => { captured = null; });
  Object.assign(canvas, { setPointerCapture: capture, releasePointerCapture: release, hasPointerCapture: (id: number) => captured === id });
  return {
    canvas, capture, release,
    down: (element: Element, x: number, y: number) => fireEvent.pointerDown(element, { ...POINTER, clientX: x, clientY: y }),
    move: (x: number, y: number) => fireEvent.pointerMove(canvas, { ...POINTER, clientX: x, clientY: y }),
    up: (x: number, y: number) => fireEvent.pointerUp(canvas, { ...POINTER, clientX: x, clientY: y }),
    cancel: () => fireEvent.pointerCancel(canvas, POINTER),
    loseCapture: () => fireEvent.lostPointerCapture(canvas, POINTER),
  };
}

export function measureTile(container: HTMLElement, id: string, rect: FloatingRect) {
  const tile = [...container.querySelectorAll<HTMLElement>('[data-dock-anchor]')].find((item) => item.dataset.dockAnchor === id)!;
  const header = tile.querySelector<HTMLElement>(':scope > [data-dock-strip]')!;
  measure(tile, rect);
  measure(header, { ...rect, height: 36 });
  return tile;
}
