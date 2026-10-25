import { useState, type ReactNode } from 'react';
import { Panel, usePanelRef } from 'react-resizable-panels';

export interface CollapsibleRegionProps {
  id: string;
  title: string;
  defaultSize: string;
  minSize: string;
  children: ReactNode;
}

const COLLAPSED_SIZE_PX = 28;

export function CollapsibleRegion({ id, title, defaultSize, minSize, children }: CollapsibleRegionProps) {
  const panelRef = usePanelRef();
  const [collapsed, setCollapsed] = useState(false);

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
          <span>{title}</span>
          <button
            type="button"
            aria-expanded={!collapsed}
            aria-label={`${collapsed ? 'Expand' : 'Collapse'} ${title}`}
            onClick={toggle}
          >
            {collapsed ? '+' : '-'}
          </button>
        </header>
        {collapsed ? null : <div className="region-body">{children}</div>}
      </section>
    </Panel>
  );
}
