import { useCallback, useLayoutEffect, useState, type ComponentType, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import type { IconProps } from '../icons/Icons';
import { DockLayout } from './DockLayout';
import { FloatingWindow } from './FloatingWindow';
import { useWorkspacePanels } from './WorkspacePanelProvider';

interface MovablePanelProps {
  id: string;
  slotId?: string;
  title: string;
  Icon: ComponentType<IconProps>;
  active?: boolean;
  onClose?: () => void;
  children: ReactNode;
}

export function MovablePanel(props: MovablePanelProps) {
  if (useWorkspacePanels() === null) return props.children;
  return <RegisteredPanel {...props} />;
}

function RegisteredPanel({ id, slotId = id, title, Icon, active = true, onClose, children }: MovablePanelProps) {
  const panels = useWorkspacePanels();
  const registerPanel = panels?.registerPanel;
  const updateTitle = panels?.updateTitle;
  const placement = panels?.placements.get(id);
  const tiledTarget = placement?.mode === 'tiled' ? panels?.slots.get(placement.slotId) ?? panels?.slots.get(slotId) : undefined;
  const [host] = useState(() => {
    const element = document.createElement('div');
    element.className = 'workspace-panel-host';
    element.dataset.workspacePanelContent = id;
    return element;
  });

  const attach = useCallback((target: HTMLElement | null) => {
    if (target !== null && host.parentNode !== target) target.appendChild(host);
  }, [host]);

  useLayoutEffect(() => registerPanel?.(id, slotId), [registerPanel, id, slotId]);
  useLayoutEffect(() => { updateTitle?.(id, title); }, [id, title, updateTitle]);
  useLayoutEffect(() => { attach(tiledTarget ?? null); }, [attach, tiledTarget]);
  useLayoutEffect(() => () => host.remove(), [host]);

  const dock = () => panels?.dockPanel(id);
  const layer = panels?.floatingLayer;

  return (
    <>
      <DockLayout origin={{ slotId, panelId: id }} hidden={!active} />
      {createPortal(children, host)}
      {placement?.mode === 'floating' && layer != null ? createPortal(
        <FloatingWindow
          panelId={id}
          title={title}
          Icon={Icon}
          rect={placement.rect}
          zIndex={panels?.floatingCanvas.zIndices.get(id) ?? 0}
          bodyRef={attach}
          onDock={dock}
          onClose={onClose ?? dock}
        />,
        layer,
      ) : null}
    </>
  );
}
