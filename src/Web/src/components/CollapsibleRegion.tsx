import { useEffect, useState, type ReactNode } from 'react';
import { Panel, usePanelRef } from 'react-resizable-panels';
import { isPanelCollapsed, recordPanelCollapsed } from '../state/workspaceLayout';
import { ChevronDownIcon, ChevronRightIcon } from './icons/Icons';

export interface CollapsibleRegionProps {
  id: string;
  title: string;
  defaultSize: string;
  minSize: string;
  keepMounted?: boolean;
  children: ReactNode;
}

const COLLAPSED_SIZE_PX = 28;

export function CollapsibleRegion({
  id,
  title,
  defaultSize,
  minSize,
  keepMounted = false,
  children,
}: CollapsibleRegionProps) {
  const panelRef = usePanelRef();
  const [collapsed, setCollapsed] = useState(() => isPanelCollapsed(id));

  useEffect(() => {
    if (isPanelCollapsed(id)) {
      panelRef.current?.collapse();
    }
  }, [id, panelRef]);

  useEffect(() => {
    recordPanelCollapsed(id, collapsed);
  }, [id, collapsed]);

  const toggle = () => {
    const panel = panelRef.current;
    if (collapsed) {
      panel?.expand();
    } else {
      panel?.collapse();
    }
    setCollapsed(!collapsed);
  };

  const syncCollapsedState = () => {
    const panel = panelRef.current;
    if (panel) {
      setCollapsed(panel.isCollapsed());
    }
  };

  return (
    <Panel
      id={id}
      panelRef={panelRef}
      collapsible
      collapsedSize={COLLAPSED_SIZE_PX}
      defaultSize={defaultSize}
      minSize={minSize}
      onResize={syncCollapsedState}
      className="region"
    >
      <section aria-label={title} className="region-content">
        <header className="region-header">
          <span className="section-label">{title}</span>
          <button
            type="button"
            className="icon-btn icon-btn-sm"
            aria-expanded={!collapsed}
            aria-label={`${collapsed ? 'Expand' : 'Collapse'} ${title}`}
            onClick={toggle}
          >
            {collapsed ? <ChevronRightIcon size={13} /> : <ChevronDownIcon size={13} />}
          </button>
        </header>
        {collapsed && !keepMounted ? null : (
          <div className="region-body" hidden={collapsed}>
            {children}
          </div>
        )}
      </section>
    </Panel>
  );
}
