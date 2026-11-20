import { describe, expect, it } from 'vitest';
import type { FloatingRect } from './floatingGeometry';
import { placeFloatingRect } from './floatingPlacement';

const viewport = { x: 0, y: 0, width: 1000, height: 800 };
const preferred = { x: 400, y: 200, width: 240, height: 160 };

describe('floating placement', () => {
  it('uses the preferred position in an empty viewport', () => {
    expect(placeFloatingRect({ preferred, viewport, occupied: [] })).toEqual(preferred);
  });

  it('allows touching occupied edges and leaves a free preferred position alone', () => {
    const occupied = [{ ...preferred, x: 160 }, { ...preferred, y: 360 }];
    expect(placeFloatingRect({ preferred, viewport, occupied })).toEqual(preferred);
  });

  it.each([
    { name: 'right', obstacle: { ...preferred, x: 260 }, result: { ...preferred, x: 500 } },
    { name: 'left', obstacle: { ...preferred, x: 540 }, result: { ...preferred, x: 300 } },
    { name: 'below', obstacle: { ...preferred, y: 60 }, result: { ...preferred, y: 220 } },
  ])('chooses the closest free position to the $name', ({ obstacle, result }) => {
    expect(placeFloatingRect({ preferred, viewport, occupied: [obstacle] })).toEqual(result);
  });

  it('breaks equal-distance ties to the right, then left, then below', () => {
    const obstacle = { ...preferred, y: 120, height: 320 };
    expect(placeFloatingRect({ preferred, viewport, occupied: [obstacle] })).toEqual({ ...preferred, x: 640 });
    const narrow = { ...viewport, width: 800 };
    expect(placeFloatingRect({ preferred, viewport: narrow, occupied: [obstacle] })).toEqual({ ...preferred, x: 160 });
  });

  it('projects the preferred x into gaps after combining all intersecting spans', () => {
    const occupied = [
      { x: 0, y: 100, width: 340, height: 400 },
      { x: 250, y: 160, width: 300, height: 320 },
      { x: 820, y: 210, width: 180, height: 290 },
    ];
    expect(placeFloatingRect({ preferred, viewport, occupied })).toEqual({ ...preferred, x: 550 });
  });

  it('accounts for both axes of viewport scroll and prefers a completely visible position', () => {
    const scrolled = { ...viewport, x: 300, y: 500 };
    const occupied = [{ x: 300, y: 340, width: 240, height: 160 }];
    expect(placeFloatingRect({ preferred, viewport: scrolled, occupied })).toEqual({ ...preferred, y: 500 });
  });

  it('continues below a full viewport without moving the occupied windows', () => {
    const occupied: readonly FloatingRect[] = [Object.freeze({ ...viewport })];
    expect(placeFloatingRect({ preferred, viewport, occupied })).toEqual({ ...preferred, y: 800 });
    expect(occupied).toEqual([viewport]);
  });

  it('starts below the viewport and clears occupied rows already in the overflow area', () => {
    const occupied = [viewport, { ...viewport, y: 800 }];
    expect(placeFloatingRect({ preferred, viewport, occupied })).toEqual({ ...preferred, y: 1600 });
  });

  it('preserves the dimensions of an oversized window and clears occupied rows', () => {
    const oversized = { ...preferred, width: 1150, height: 950 };
    const occupied = [{ ...preferred, y: 0 }];
    expect(placeFloatingRect({ preferred: oversized, viewport, occupied })).toEqual({
      ...oversized, x: 0, y: 160,
    });
  });
});
