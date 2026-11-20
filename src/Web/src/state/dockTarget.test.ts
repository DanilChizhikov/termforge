import { describe, expect, it } from 'vitest';
import { resolveDockTarget, WORKSPACE_ROOT } from './dockTarget';

const viewport = { x: 100, y: 50, width: 1000, height: 800 };
const pane = {
  id: 'terminal',
  rect: { x: 350, y: 200, width: 450, height: 350 },
  headers: [{ x: 350, y: 200, width: 450, height: 42 }, { x: 430, y: 145, width: 160, height: 40 }],
};

describe('dock target resolution', () => {
  it.each([
    ['left', { x: 108, y: 400 }], ['right', { x: 1091, y: 400 }],
    ['top', { x: 450, y: 58 }], ['bottom', { x: 450, y: 845 }],
  ])('resolves the workspace %s edge', (side, pointer) => {
    expect(resolveDockTarget({ pointer, viewport, slots: [pane] })).toEqual({
      kind: 'split', anchorId: WORKSPACE_ROOT, side,
    });
  });

  it.each([
    ['left', { x: 357, y: 340 }], ['right', { x: 795, y: 340 }],
    ['top', { x: 645, y: 203 }], ['bottom', { x: 645, y: 541 }],
  ])('resolves the pane %s edge', (side, pointer) => {
    expect(resolveDockTarget({ pointer, viewport, slots: [pane] })).toEqual({
      kind: 'split', anchorId: pane.id, side,
    });
  });

  it('uses the closest edge and resolves equal distances deterministically', () => {
    expect(resolveDockTarget({ pointer: { x: 111, y: 53 }, viewport, slots: [] })).toMatchObject({ side: 'top' });
    expect(resolveDockTarget({ pointer: { x: 108, y: 58 }, viewport, slots: [] })).toMatchObject({ side: 'left' });
    const overlapping = { ...pane, id: 'nearer', rect: { ...pane.rect, x: 355 } };
    expect(resolveDockTarget({ pointer: { x: 356, y: 340 }, viewport, slots: [pane, overlapping] }))
      .toMatchObject({ anchorId: 'nearer', side: 'left' });
  });

  it('prioritizes the root over a pane edge and a pane edge over its tab strip', () => {
    const touchingRoot = { ...pane, rect: { ...pane.rect, x: viewport.x } };
    expect(resolveDockTarget({ pointer: { x: 106, y: 207 }, viewport, slots: [touchingRoot] }))
      .toEqual({ kind: 'split', anchorId: WORKSPACE_ROOT, side: 'left' });
    expect(resolveDockTarget({ pointer: { x: 490, y: 207 }, viewport, slots: [pane] }))
      .toEqual({ kind: 'split', anchorId: pane.id, side: 'top' });
  });

  it.each([{ x: 430, y: 228 }, { x: 480, y: 160 }])('uses the center of a header or tab strip', (pointer) => {
    expect(resolveDockTarget({ pointer, viewport, slots: [pane] })).toEqual({ kind: 'tab', slotId: pane.id });
  });

  it.each([{ x: 550, y: 400 }, { x: 90, y: 228 }, { x: 280, y: 370 }])('leaves an untargeted point floating', (pointer) => {
    expect(resolveDockTarget({ pointer, viewport, slots: [pane] })).toBeNull();
  });
});
