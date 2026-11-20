export interface FloatingRect {
  x: number;
  y: number;
  width: number;
  height: number;
}

export const RESIZE_DIRECTIONS = ['n', 'ne', 'e', 'se', 's', 'sw', 'w', 'nw'] as const;
export type FloatingDirection = 'move' | (typeof RESIZE_DIRECTIONS)[number];

interface PointerDelta {
  x: number;
  y: number;
}

const MIN_WIDTH = 240;
const MIN_HEIGHT = 144;

export function normalizeFloatingRect(rect: FloatingRect): FloatingRect | null {
  if (![rect.x, rect.y, rect.width, rect.height].every(Number.isFinite)) {
    return null;
  }
  return { ...rect, width: Math.max(MIN_WIDTH, rect.width), height: Math.max(MIN_HEIGHT, rect.height) };
}

export function adjustFloatingRect(rect: FloatingRect, direction: FloatingDirection, delta: PointerDelta): FloatingRect {
  if (direction === 'move') {
    return { ...rect, x: rect.x + delta.x, y: rect.y + delta.y };
  }

  const west = direction.includes('w');
  const north = direction.includes('n');
  const widthDelta = west ? -delta.x : direction.includes('e') ? delta.x : 0;
  const heightDelta = north ? -delta.y : direction.includes('s') ? delta.y : 0;
  const width = Math.max(MIN_WIDTH, rect.width + widthDelta);
  const height = Math.max(MIN_HEIGHT, rect.height + heightDelta);
  return {
    x: west ? rect.x + rect.width - width : rect.x,
    y: north ? rect.y + rect.height - height : rect.y,
    width,
    height,
  };
}
