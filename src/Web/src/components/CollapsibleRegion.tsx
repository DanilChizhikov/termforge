import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
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

interface RegionToggleState {
  title: string;
  collapsed: boolean;
  toggle: () => void;
}

const COLLAPSED_SIZE_PX = 28;

const RegionToggleContext = createContext<RegionToggleState | null>(null);

export function RegionToggle() {
  const region = useContext(RegionToggleContext);
  if (region === null) {
    return null;
  }
  return (
    <button
      type="button"
      className="icon-btn icon-btn-sm region-toggle"
      aria-expanded={!region.collapsed}
      aria-label={`${region.collapsed ? 'Expand' : 'Collapse'} ${region.title}`}
      onClick={region.toggle}
    >
      {region.collapsed ? <ChevronRightIcon size={13} /> : <ChevronDownIcon size={13} />}
    </button>
  );
}

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

  const toggleState: RegionToggleState = { title, collapsed, toggle };

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
      <RegionToggleContext.Provider value={toggleState}>
        <section aria-label={title} className="region-content">
          {collapsed ? (
            <header className="region-header">
              <span className="section-label">{title}</span>
              <RegionToggle />
            </header>
          ) : null}
          {collapsed && !keepMounted ? null : (
            <div className="region-body" hidden={collapsed}>
              {children}
            </div>
          )}
        </section>
      </RegionToggleContext.Provider>
    </Panel>
  );
}
