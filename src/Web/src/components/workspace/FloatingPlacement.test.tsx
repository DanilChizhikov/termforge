import { act, fireEvent, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import type { FloatingRect } from '../../state/floatingGeometry';
import { CANVAS_RECT, dockPointer } from '../../test/DockingPointer';
import { renderInWorkspace } from '../../test/FloatingTestWorkspace';
import { RecordingWarningDialog } from '../RecordingWarningDialog';
import { FileIcon } from '../icons/Icons';
import { MovablePanel } from './MovablePanel';

const FIRST = { x: 0, y: 120, width: 240, height: 200 };
const SECOND = { ...FIRST, x: 240 };

function RecordingAction() {
  const [open, setOpen] = useState(false);
  return (
    <>
      <button type="button" onClick={() => setOpen(true)}>Record</button>
      {open ? (
        <RecordingWarningDialog title="Record this terminal?" confirmLabel="Start" onConfirm={vi.fn()} onCancel={() => setOpen(false)} />
      ) : null}
    </>
  );
}

function pair() {
  const view = renderInWorkspace(
    <>
      <MovablePanel id="first" title="First panel" Icon={FileIcon}>
        <RecordingAction />
        <input aria-label="First draft" />
      </MovablePanel>
      <MovablePanel id="second" title="Second panel" Icon={FileIcon}>
        <input aria-label="Second draft" />
      </MovablePanel>
    </>,
  );
  const pointer = dockPointer(view.container);
  const detach = (id: string, rect: FloatingRect) => act(() => view.panels().detachPanel(id, rect));
  const cssRule = (element: Element) => element.querySelector('style')?.sheet?.cssRules[0] as CSSStyleRule;
  return {
    pointer, detach, panels: view.panels,
    first: () => screen.getByRole('region', { name: 'First panel' }),
    second: () => screen.getByRole('region', { name: 'Second panel' }),
    extent: () => cssRule(pointer.canvas.querySelector('.workspace-floating-layer')!).style,
    zIndex: (element: Element) => cssRule(element).style.getPropertyValue('--window-z'),
  };
}

describe('floating workspace placement', () => {
  it('adds scrollable extent when the viewport is full and removes it when the window docks', () => {
    const { detach, panels, second, extent } = pair();
    const full = { x: 0, y: 0, width: CANVAS_RECT.width, height: CANVAS_RECT.height };
    detach('first', full);
    detach('second', SECOND);

    expect(panels().placements.get('first')).toMatchObject({ rect: full });
    expect(panels().placements.get('second')).toMatchObject({ rect: { ...SECOND, y: full.height } });
    expect(extent().getPropertyValue('--canvas-height')).toBe('1000px');

    fireEvent.click(within(second()).getByRole('button', { name: 'Dock' }));
    expect(extent().getPropertyValue('--canvas-height')).toBe('800px');
  });

  it('resolves a resize collision only when the resize is committed', () => {
    const { detach, first, panels, pointer, extent } = pair();
    detach('first', FIRST);
    detach('second', SECOND);
    const resize = first().querySelector('[data-resize-direction="e"]')!;

    pointer.down(resize, 290, 210);
    pointer.move(410, 210);
    expect(panels().placements.get('first')).toMatchObject({ rect: FIRST });
    expect(extent().getPropertyValue('--canvas-width')).toBe('480px');
    pointer.cancel();
    expect(panels().placements.get('first')).toMatchObject({ rect: FIRST });

    pointer.down(resize, 290, 210);
    pointer.up(410, 210);
    expect(panels().placements.get('first')).toMatchObject({ rect: { ...FIRST, y: 320, width: 360 } });
    expect(panels().placements.get('second')).toMatchObject({ rect: SECOND });
    expect(extent().getPropertyValue('--canvas-height')).toBe('520px');
  });

  it('only makes the dragged window translucent and leaves the extent and neighbors alone until drop', () => {
    const { detach, first, second, panels, pointer, extent } = pair();
    detach('first', FIRST);
    detach('second', SECOND);
    const title = first().querySelector('.floating-window-title')!;
    pointer.down(title, 130, 180);
    pointer.move(1600, 1100);

    expect(first()).toHaveAttribute('data-dragging');
    expect(second()).not.toHaveAttribute('data-dragging');
    expect(pointer.canvas.querySelector('.dialog-backdrop')).toBeNull();
    expect(extent().getPropertyValue('--canvas-width')).toBe('480px');
    expect(extent().getPropertyValue('--canvas-height')).toBe('320px');
    expect(panels().placements.get('second')).toMatchObject({ rect: SECOND });

    pointer.up(1600, 1100);
    expect(first()).not.toHaveAttribute('data-dragging');
    expect(panels().placements.get('first')).toMatchObject({ rect: { ...FIRST, x: 760, y: 600 } });
    expect(panels().placements.get('second')).toMatchObject({ rect: SECOND });
  });

  it('places an oversized window using the visible canvas origin', () => {
    const { detach, panels, pointer, extent } = pair();
    pointer.canvas.scrollLeft = 120;
    pointer.canvas.scrollTop = 180;
    const oversized = { x: 0, y: 0, width: 1100, height: 900 };
    detach('first', oversized);
    expect(panels().placements.get('first')).toMatchObject({ rect: { ...oversized, x: 120, y: 180 } });
    expect(extent().getPropertyValue('--canvas-width')).toBe('1220px');
    expect(extent().getPropertyValue('--canvas-height')).toBe('1080px');
  });

  it('raises windows on mouse and keyboard focus without remounting their content', async () => {
    const user = userEvent.setup();
    const { detach, first, second, zIndex } = pair();
    detach('first', FIRST);
    detach('second', SECOND);
    const draft = screen.getByRole('textbox', { name: 'First draft' });
    expect(zIndex(second())).toBe('2');
    await user.click(draft);
    await user.keyboard('draft');
    expect(zIndex(first())).toBe('2');
    expect(zIndex(second())).toBe('1');

    await user.tab();
    expect(within(second()).getByRole('button', { name: 'Dock' })).toHaveFocus();
    expect(zIndex(second())).toBe('2');
    expect(screen.getByRole('textbox', { name: 'First draft' })).toBe(draft);
    expect(draft).toHaveValue('draft');
  });

  it('renders a modal outside the floating layer without promoting its originating panel', () => {
    const { detach, first, second, zIndex } = pair();
    detach('first', FIRST);
    detach('second', SECOND);
    fireEvent.click(within(first()).getByRole('button', { name: 'Record' }));

    const dialog = screen.getByRole('dialog', { name: 'Record this terminal?' });
    expect(dialog.parentElement?.parentElement).toBe(document.body);
    expect(within(dialog).getByRole('button', { name: 'Cancel' })).toHaveFocus();
    expect(zIndex(second())).toBe('2');
  });
});
