import { act, fireEvent, screen, within } from '@testing-library/react';
import { useEffect, useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { INITIAL_FLOATING_RECT, renderInWorkspace } from '../../test/FloatingTestWorkspace';
import { FileIcon } from '../icons/Icons';
import { MovablePanel } from './MovablePanel';
import { PanelSlot } from './PanelSlot';

function StatefulInput({ mounted, unmounted }: { mounted: () => void; unmounted: () => void }) {
  const [value, setValue] = useState('');
  useEffect(() => { mounted(); return unmounted; }, [mounted, unmounted]);
  return <input aria-label="Draft" value={value} onChange={(event) => setValue(event.target.value)} />;
}

function capturePointer(canvas: HTMLElement) {
  let captured: number | null = null;
  const setPointerCapture = vi.fn((id: number) => { captured = id; });
  const releasePointerCapture = vi.fn(() => { captured = null; });
  Object.assign(canvas, { setPointerCapture, releasePointerCapture, hasPointerCapture: (id: number) => captured === id });
  return { setPointerCapture, releasePointerCapture };
}

function exampleWindow() {
  const view = renderInWorkspace(<MovablePanel id="example" title="Example" Icon={FileIcon}>Contents</MovablePanel>);
  view.detach('example');
  const window = screen.getByRole('region', { name: 'Example' });
  const canvas = view.container.querySelector<HTMLElement>('.workspace-canvas')!;
  const style = window.querySelector('style')?.sheet?.cssRules[0] as CSSStyleRule;
  return { window, canvas, style, panels: view.panels };
}

const DOWN = { pointerId: 7, isPrimary: true, button: 0, clientX: 100, clientY: 100 };
const MOVE = { pointerId: 7, clientX: 130, clientY: 140 };

describe('floating windows', () => {
  it('moves the same stateful DOM subtree between its original slot, floating body and another slot', () => {
    const mounted = vi.fn();
    const unmounted = vi.fn();
    const { panels, detach, container } = renderInWorkspace(
      <>
        <div aria-label="Original tile">
          <MovablePanel id="example" title="Example" Icon={FileIcon}>
            <StatefulInput mounted={mounted} unmounted={unmounted} />
          </MovablePanel>
        </div>
        <div aria-label="Other tile"><PanelSlot id="other" /></div>
      </>,
    );
    const input = screen.getByRole('textbox', { name: 'Draft' });
    fireEvent.change(input, { target: { value: 'draft' } });
    detach('example');
    const floating = screen.getByRole('region', { name: 'Example' });
    expect(floating).toContainElement(input);
    expect(input).toHaveValue('draft');
    fireEvent.click(within(floating).getByRole('button', { name: 'Dock' }));
    expect(container.querySelector('[aria-label="Original tile"]')).toContainElement(input);

    detach('example');
    act(() => panels().dockPanel('example', 'other'));
    expect(container.querySelector('[aria-label="Other tile"]')).toContainElement(input);
    expect(input).toHaveValue('draft');
    expect(mounted).toHaveBeenCalledOnce();
    expect(unmounted).not.toHaveBeenCalled();
  });

  it('captures the pointer on the canvas and commits title-bar movement on pointerup', () => {
    const { window, canvas, style, panels } = exampleWindow();
    const capture = capturePointer(canvas);
    fireEvent.pointerDown(window.querySelector('.floating-window-title')!, DOWN);
    fireEvent.pointerMove(canvas, { ...MOVE, pointerId: 8 });
    expect(style.style.getPropertyValue('--window-x')).toBe('16px');
    fireEvent.pointerMove(canvas, MOVE);
    expect(style.style.getPropertyValue('--window-x')).toBe('46px');
    expect(panels().placements.get('example')).toMatchObject({ rect: INITIAL_FLOATING_RECT });
    fireEvent(window, new PointerEvent('lostpointercapture', { bubbles: true, pointerId: 7 }));
    expect(style.style.getPropertyValue('--window-x')).toBe('46px');

    fireEvent.pointerUp(canvas, MOVE);
    expect(capture.setPointerCapture).toHaveBeenCalledWith(7);
    expect(capture.releasePointerCapture).toHaveBeenCalledWith(7);
    expect(panels().placements.get('example')).toMatchObject({ rect: { ...INITIAL_FLOATING_RECT, x: 46, y: 64 } });
  });

  it.each(['pointercancel', 'lostpointercapture'])('rolls back a resize on %s', (event) => {
    const { window, canvas, style, panels } = exampleWindow();
    capturePointer(canvas);
    expect(window.querySelectorAll('[data-resize-direction]')).toHaveLength(8);
    fireEvent.pointerDown(window.querySelector('[data-resize-direction="nw"]')!, DOWN);
    fireEvent.pointerMove(canvas, MOVE);
    expect(style.style.getPropertyValue('--window-width')).toBe('450px');
    expect(style.style.getPropertyValue('--window-height')).toBe('280px');

    fireEvent(canvas, new PointerEvent(event, { bubbles: true, pointerId: 7 }));
    expect(style.style.getPropertyValue('--window-width')).toBe('480px');
    expect(style.style.getPropertyValue('--window-height')).toBe('320px');
    expect(panels().placements.get('example')).toMatchObject({ rect: INITIAL_FLOATING_RECT });
  });
});
