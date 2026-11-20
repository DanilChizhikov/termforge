import { createContext, useRef, type ReactNode } from 'react';
import { useFloatingGesture, type StartFloatingGesture } from './useFloatingGesture';
import { useWorkspacePanels } from './WorkspacePanelProvider';

export const FloatingGestureContext = createContext<StartFloatingGesture | null>(null);

export function WorkspaceCanvas({ hidden, children }: { hidden?: boolean; children: ReactNode }) {
  const panels = useWorkspacePanels();
  const canvas = useRef<HTMLDivElement>(null);
  const { start, move, finish } = useFloatingGesture(canvas, (id, rect) => panels?.commitFloatingRect(id, rect));

  return (
    <FloatingGestureContext.Provider value={start}>
      <div
        ref={canvas}
        className="app-view workspace-canvas"
        hidden={hidden}
        onPointerMove={move}
        onPointerUp={(event) => finish(event, true)}
        onPointerCancel={(event) => finish(event, false)}
        onLostPointerCapture={(event) => { if (event.target === event.currentTarget) finish(event, false); }}
      >
        {children}
        <div ref={panels?.setFloatingLayer} className="workspace-floating-layer" />
      </div>
    </FloatingGestureContext.Provider>
  );
}
