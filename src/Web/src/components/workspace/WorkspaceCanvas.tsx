import { createContext, useCallback, useId, useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { WORKSPACE_ROOT } from '../../state/dockTarget';
import type { FloatingRect } from '../../state/floatingGeometry';
import { DockLayout } from './DockLayout';
import { useFloatingGesture, type StartFloatingGesture } from './useFloatingGesture';
import { useWorkspacePanels } from './WorkspacePanelProvider';

export const FloatingGestureContext = createContext<StartFloatingGesture | null>(null);

export function WorkspaceCanvas({ hidden, children }: { hidden?: boolean; children: ReactNode }) {
  const panels = useWorkspacePanels();
  const canvas = useRef<HTMLDivElement>(null);
  const { start, preview, lostCapture } = useFloatingGesture(canvas, panels);
  const [host] = useState(() => {
    const element = document.createElement('div');
    element.className = 'workspace-tiled-host';
    return element;
  });
  const attach = useCallback((element: HTMLDivElement | null) => {
    if (element !== null && host.parentNode !== element) element.appendChild(host);
  }, [host]);
  useLayoutEffect(() => () => host.remove(), [host]);

  return (
    <FloatingGestureContext.Provider value={start}>
      <div
        ref={canvas}
        className="app-view workspace-canvas"
        hidden={hidden}
        onLostPointerCapture={lostCapture}
      >
        <DockLayout origin={{ slotId: WORKSPACE_ROOT, root: <div className="workspace-tiled-host" ref={attach} /> }} />
        {createPortal(children, host)}
        <div ref={panels?.setFloatingLayer} className="workspace-floating-layer" />
        {preview.dock === null ? null : <DragOverlay kind="target" rect={preview.dock.rect} />}
        {preview.ghost === null ? null : <DragOverlay kind="ghost" rect={preview.ghost} />}
      </div>
    </FloatingGestureContext.Provider>
  );
}

function DragOverlay({ kind, rect }: { kind: 'target' | 'ghost'; rect: FloatingRect }) {
  const id = `dock-preview-${useId().replace(/[^a-z0-9_-]/gi, '')}`;
  return (
    <div id={id} aria-hidden="true" className={`workspace-dock-overlay workspace-dock-overlay-${kind}`}>
      <style>{`#${id} { left: ${rect.x}px; top: ${rect.y}px; width: ${rect.width}px; height: ${rect.height}px; }`}</style>
    </div>
  );
}
