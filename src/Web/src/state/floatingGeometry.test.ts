import { describe, expect, it } from 'vitest';
import { adjustFloatingRect, normalizeFloatingRect } from './floatingGeometry';

const RECT = { x: 25, y: 40, width: 400, height: 240 };

describe('floating geometry', () => {
  it.each([
    ['n', { x: 25, y: 52, width: 400, height: 228 }],
    ['ne', { x: 25, y: 52, width: 430, height: 228 }],
    ['e', { x: 25, y: 40, width: 430, height: 240 }],
    ['se', { x: 25, y: 40, width: 430, height: 252 }],
    ['s', { x: 25, y: 40, width: 400, height: 252 }],
    ['sw', { x: 55, y: 40, width: 370, height: 252 }],
    ['w', { x: 55, y: 40, width: 370, height: 240 }],
    ['nw', { x: 55, y: 52, width: 370, height: 228 }],
  ] as const)('resizes from %s', (direction, expected) => {
    expect(adjustFloatingRect(RECT, direction, { x: 30, y: 12 })).toEqual(expected);
  });

  it('preserves the southeast corner when the northwest handle reaches the minimum size', () => {
    const resized = adjustFloatingRect(RECT, 'nw', { x: 999, y: 999 });
    expect(resized).toEqual({ x: 185, y: 136, width: 240, height: 144 });
  });

  it('rejects non-finite coordinates', () => {
    expect(normalizeFloatingRect({ ...RECT, x: Number.NaN })).toBeNull();
  });
});
