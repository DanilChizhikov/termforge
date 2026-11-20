import { createContext, useCallback, useId, useLayoutEffect, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { WORKSPACE_ROOT } from '../../state/dockTarget';
import type { FloatingRect } from '../../state/floatingGeometry';
import { DockLayout } from './DockLayout';
import { useFloatingGesture, type StartFloatingGesture } from './useFloatingGesture';
import { useWorkspacePanels } from './WorkspacePanelProvider';

export const FloatingGestureContext = createContext<StartFloatingGesture | null>(null);

export function WorkspaceCanvas({ hidden, children }: { hidden?: boolean; children: ReactNode }) {
  const panels = useWorkspacePanels();
  const [canvas, setCanvas] = useState<HTMLDivElement | null>(null);
  const { start, preview, lostCapture } = useFloatingGesture(canvas, panels);
  const layerId = `floating-layer-${useId().replace(/[^a-z0-9_-]/gi, '')}`;
  const { width = 0, height = 0 } = panels?.floatingCanvas ?? {};
  const floatingLayer = panels?.floatingLayer;
  const raisePanel = panels?.raisePanel;
  const [host] = useState(() => {
    const element = document.createElement('div');
    element.className = 'workspace-tiled-host';
    return element;
  });
  const attach = useCallback((element: HTMLDivElement | null) => {
    if (element !== null && host.parentNode !== element) element.appendChild(host);
  }, [host]);
  useLayoutEffect(() => () => host.remove(), [host]);
  useLayoutEffect(() => {
    if (floatingLayer == null || raisePanel === undefined) return;
    const focus = (event: FocusEvent) => {
      if (!(event.target instanceof Element)) return;
      const panelId = event.target.closest<HTMLElement>('[data-floating-panel]')?.dataset.floatingPanel;
      if (panelId !== undefined) raisePanel(panelId);
    };
    floatingLayer.addEventListener('focusin', focus);
    return () => floatingLayer.removeEventListener('focusin', focus);
  }, [floatingLayer, raisePanel]);

  return (
    <FloatingGestureContext.Provider value={start}>
      <div
        ref={setCanvas}
        className="app-view workspace-canvas"
        hidden={hidden}
        onLostPointerCapture={lostCapture}
      >
        <DockLayout origin={{ slotId: WORKSPACE_ROOT, root: <div className="workspace-tiled-host" ref={attach} /> }} />
        {createPortal(children, host)}
        <div id={layerId} className="workspace-floating-layer">
          <style>{`#${layerId} { --canvas-width: ${width}px; --canvas-height: ${height}px; }`}</style>
          <div ref={panels?.setFloatingLayer} className="workspace-floating-windows" />
          {preview.dock === null ? null : <DragOverlay kind="target" rect={preview.dock.rect} />}
          {preview.ghost === null ? null : <DragOverlay kind="ghost" rect={preview.ghost} />}
        </div>
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
