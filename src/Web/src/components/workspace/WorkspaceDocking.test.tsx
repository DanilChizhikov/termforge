import { act, fireEvent, screen, within } from '@testing-library/react';
import { useEffect, useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { WORKSPACE_ROOT } from '../../state/dockTarget';
import { getWorkspaceLayout, recordPanelLayout } from '../../state/workspaceLayout';
import { dockPointer, measure, measureTile } from '../../test/DockingPointer';
import { renderInWorkspace } from '../../test/FloatingTestWorkspace';
import { FileIcon } from '../icons/Icons';
import { MovablePanel } from './MovablePanel';
import { useDockTab } from './useDockTab';

const TILE = { x: 500, y: 250, width: 400, height: 400 };
const STRIP = { x: 150, y: 130, width: 300, height: 36 };

function DockExampleTabs({ onSelect }: { onSelect: () => void }) {
  const tab = useDockTab();
  return (
    <div role="tablist" aria-label="Example tabs">
      <button type="button" role="tab" onClick={onSelect} {...tab('source')}>Source tab</button>
    </div>
  );
}

function StatefulDraft({ mount, unmount }: { mount: () => void; unmount: () => void }) {
  const [value, setValue] = useState('');
  useEffect(() => { mount(); return unmount; }, [mount, unmount]);
  return <input aria-label="Draft" value={value} onChange={(event) => setValue(event.target.value)} />;
}

function example() {
  const selected = vi.fn();
  const mount = vi.fn();
  const unmount = vi.fn();
  const view = renderInWorkspace(
    <>
      <DockExampleTabs onSelect={selected} />
      <MovablePanel id="source" title="Source" Icon={FileIcon}><StatefulDraft mount={mount} unmount={unmount} /></MovablePanel>
      <MovablePanel id="target" title="Target" Icon={FileIcon}>Target contents</MovablePanel>
    </>,
  );
  measure(screen.getByRole('tablist', { name: 'Example tabs' }), STRIP);
  const target = measureTile(view.container, 'target', TILE);
  const pointer = dockPointer(view.container);
  const draft = screen.getByRole('textbox', { name: 'Draft' });
  fireEvent.change(draft, { target: { value: 'unsaved draft' } });
  const floating = () => screen.getByRole('region', { name: 'Source' });
  const title = () => floating().querySelector('.floating-window-title')!;
  return { ...view, pointer, target, draft, selected, mount, unmount, floating, title };
}

describe('workspace docking', () => {
  it('detaches with the tiled header button and docks back to its original slot', () => {
    const { draft, floating, mount, unmount } = example();
    fireEvent.click(screen.getByRole('button', { name: 'Detach Source' }));
    expect(floating()).toContainElement(draft);
    fireEvent.click(within(floating()).getByRole('button', { name: 'Dock' }));
    expect(draft.closest('[data-workspace-slot]')).toHaveAttribute('data-workspace-slot', 'source');
    expect(draft).toHaveValue('unsaved draft');
    expect(mount).toHaveBeenCalledOnce();
    expect(unmount).not.toHaveBeenCalled();
  });

  it('preserves ordinary clicks and starts a floating drag only after leaving the original strip', () => {
    const { pointer, floating, draft, selected, panels } = example();
    const tab = screen.getByRole('tab', { name: 'Source tab' });
    pointer.down(tab, 190, 150);
    pointer.move(220, 151);
    pointer.up(220, 151);
    fireEvent.click(tab);
    expect(selected).toHaveBeenCalledOnce();
    expect(pointer.capture).not.toHaveBeenCalled();

    pointer.down(tab, 190, 150);
    pointer.move(191, 170);
    expect(pointer.capture).toHaveBeenCalledWith(7);
    expect(pointer.canvas.querySelector('.workspace-dock-overlay-ghost')).toBeInTheDocument();
    expect(panels().placements.get('source')).toEqual({ mode: 'tiled', slotId: 'source' });
    pointer.up(300, 500);
    expect(floating()).toContainElement(draft);
    expect(pointer.release).toHaveBeenCalledWith(7);
    expect(pointer.canvas.querySelector('.workspace-dock-overlay')).toBeNull();
  });

  it('creates a real root split and keeps the original layout and mounted content on collapse', () => {
    recordPanelLayout('app-layout', { servers: 25, main: 75 });
    recordPanelLayout('main-workspace', { terminals: 56, files: 44 });
    recordPanelLayout('file-panes', { 'local-files': 51, 'remote-files': 49 });
    const original = getWorkspaceLayout();
    const { pointer, draft, panels, detach, title, target, mount, unmount } = example();
    detach('source');
    pointer.down(title(), 200, 160);
    pointer.move(54, 450);
    expect(pointer.canvas.querySelector('.workspace-dock-overlay-target')).toBeInTheDocument();
    expect(screen.queryByRole('separator', { name: 'Resize docked panels' })).toBeNull();
    pointer.up(54, 450);
    expect(panels().trees.get(WORKSPACE_ROOT)).toMatchObject({ kind: 'split', orientation: 'horizontal', firstSize: 30 });
    expect(screen.getByRole('separator', { name: 'Resize docked panels' })).toBeInTheDocument();
    expect(pointer.canvas).toContainElement(target);
    expect(screen.getByRole('textbox', { name: 'Draft' })).toBe(draft);
    expect(getWorkspaceLayout()).toBe(original);

    fireEvent.click(screen.getByRole('button', { name: 'Detach Source' }));
    expect(screen.queryByRole('separator', { name: 'Resize docked panels' })).toBeNull();
    expect(pointer.canvas).toContainElement(target);
    fireEvent.click(screen.getByRole('button', { name: 'Dock' }));
    expect(draft.closest('[data-workspace-slot]')).toHaveAttribute('data-workspace-slot', 'source');
    expect(mount).toHaveBeenCalledOnce();
    expect(unmount).not.toHaveBeenCalled();
  });

  it('creates a local split without wrapping the workspace root', () => {
    const { pointer, panels, detach, title, draft } = example();
    detach('source');
    pointer.down(title(), 200, 160);
    pointer.move(700, 645);
    pointer.up(700, 645);
    expect(panels().trees.get('target')).toMatchObject({ kind: 'split', orientation: 'vertical', firstSize: 50 });
    expect(panels().trees.get(WORKSPACE_ROOT)).toBeUndefined();
    expect(draft.closest('.workspace-dock-split')).toContainElement(screen.getByText('Target contents'));
  });

  it('creates selectable docked tabs and restores the previous stack from Dock', () => {
    const { pointer, draft, detach, title, floating } = example();
    detach('source');
    pointer.down(title(), 200, 160);
    pointer.up(700, 274);
    let tablist = screen.getByRole('tablist', { name: 'Docked panels' });
    expect(within(tablist).getByRole('tab', { name: 'Source' })).toHaveAttribute('aria-selected', 'true');
    fireEvent.click(within(tablist).getByRole('tab', { name: 'Target' }));
    expect(screen.getByText('Target contents')).toBeVisible();
    expect(draft).not.toBeVisible();
    fireEvent.click(within(tablist).getByRole('tab', { name: 'Source' }));
    expect(draft).toBeVisible();
    fireEvent.click(screen.getByRole('button', { name: 'Detach Source' }));
    expect(screen.queryByRole('tablist', { name: 'Docked panels' })).toBeNull();
    fireEvent.click(within(floating()).getByRole('button', { name: 'Dock' }));
    tablist = screen.getByRole('tablist', { name: 'Docked panels' });
    expect(within(tablist).getByRole('tab', { name: 'Source' })).toHaveAttribute('aria-selected', 'true');
    expect(draft).toBeVisible();
    expect(draft).toHaveValue('unsaved draft');
  });

  it('moves docked guests to a workspace split when their original anchor closes', () => {
    function ClosableAnchor() {
      const [open, setOpen] = useState(true);
      return (
        <>
          <button type="button" onClick={() => setOpen(false)}>Close anchor</button>
          {open ? <MovablePanel id="anchor" title="Anchor" Icon={FileIcon}>Anchor contents</MovablePanel> : null}
        </>
      );
    }
    const { container, detach } = renderInWorkspace(
      <>
        <MovablePanel id="guest" title="Guest" Icon={FileIcon}><input aria-label="Guest draft" /></MovablePanel>
        <ClosableAnchor />
      </>,
    );
    const input = screen.getByRole('textbox', { name: 'Guest draft' });
    const pointer = dockPointer(container);
    const anchor = measureTile(container, 'anchor', TILE);
    detach('guest');
    const floating = screen.getByRole('region', { name: 'Guest' });
    pointer.down(floating.querySelector('.floating-window-title')!, 200, 160);
    pointer.up(700, 274);
    expect(anchor).toContainElement(input);

    fireEvent.click(screen.getByRole('button', { name: 'Close anchor' }));
    expect(anchor).not.toBeInTheDocument();
    expect(input.closest('.workspace-dock-split')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: 'Guest draft' })).toBe(input);
  });

  it.each(['escape', 'cancel', 'capture'] as const)('rolls back active drag-out on %s', (kind) => {
    const { pointer, panels, draft } = example();
    pointer.down(screen.getByRole('tab', { name: 'Source tab' }), 190, 150);
    pointer.move(700, 274);
    expect(pointer.canvas.querySelector('.workspace-dock-overlay-target')).toBeInTheDocument();
    if (kind === 'escape') fireEvent.keyDown(window, { key: 'Escape' });
    if (kind === 'cancel') pointer.cancel();
    if (kind === 'capture') pointer.loseCapture();
    pointer.up(700, 274);
    expect(pointer.canvas.querySelector('.workspace-dock-overlay')).toBeNull();
    expect(panels().placements.get('source')).toEqual({ mode: 'tiled', slotId: 'source' });
    expect(draft).toBeVisible();
  });

  it('rechecks the target on pointerup and leaves a drop onto a hidden or floating panel floating', () => {
    const { pointer, panels, detach, title, target, floating, draft } = example();
    detach('source');
    pointer.down(title(), 200, 160);
    pointer.move(700, 274);
    expect(pointer.canvas.querySelector('.workspace-dock-overlay-target')).toBeInTheDocument();
    target.hidden = true;
    pointer.up(700, 274);
    expect(panels().placements.get('source')?.mode).toBe('floating');
    target.hidden = false;
    detach('target');
    pointer.down(title(), 700, 274);
    pointer.move(710, 280);
    expect(pointer.canvas.querySelector('.workspace-dock-overlay-target')).toBeNull();
    pointer.up(710, 280);
    expect(floating()).toContainElement(draft);
    expect(panels().trees.size).toBe(0);
    act(() => panels().dockPanel('source', 'invalid'));
    expect(floating()).toContainElement(draft);
  });
});
