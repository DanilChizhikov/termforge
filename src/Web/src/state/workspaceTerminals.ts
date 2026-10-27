import type { WorkspaceTerminalTab } from '../api/workspace';
import { createTabKey, restoredTerminalTab, type TerminalTab } from './terminalTabs';

export interface RestoredTerminals {
  tabs: TerminalTab[];
  activeKey: string | null;
}

export interface TerminalTabsSnapshot {
  tabs: readonly WorkspaceTerminalTab[];
  activeKey: string | null;
}

const NOTHING_RESTORED: RestoredTerminals = { tabs: [], activeKey: null };

let restored: RestoredTerminals = NOTHING_RESTORED;
let snapshot: TerminalTabsSnapshot = { tabs: [], activeKey: null };
let listeners: Array<() => void> = [];

function sameSnapshot(left: TerminalTabsSnapshot, right: TerminalTabsSnapshot): boolean {
  return (
    left.activeKey === right.activeKey &&
    left.tabs.length === right.tabs.length &&
    left.tabs.every((tab, index) => {
      const other = right.tabs[index];
      return (
        other !== undefined &&
        tab.key === other.key &&
        tab.connectionId === other.connectionId &&
        tab.connectionName === other.connectionName
      );
    })
  );
}

export function restoreTerminalTabs(tabs: readonly WorkspaceTerminalTab[], activeKey: string | null): void {
  let restoredActiveKey: string | null = null;
  const restoredTabs = tabs.map((tab) => {
    const key = createTabKey();
    if (tab.key === activeKey) {
      restoredActiveKey = key;
    }
    return restoredTerminalTab(key, tab.connectionId, tab.connectionName);
  });
  restored = { tabs: restoredTabs, activeKey: restoredActiveKey };
}

export function getRestoredTerminals(): RestoredTerminals {
  return restored;
}

export function markRestoredTerminalsUsed(): void {
  restored = NOTHING_RESTORED;
}

export function subscribeTerminalTabs(listener: () => void): () => void {
  listeners = [...listeners, listener];
  return () => {
    listeners = listeners.filter((item) => item !== listener);
  };
}

export function getTerminalTabsSnapshot(): TerminalTabsSnapshot {
  return snapshot;
}

export function publishTerminalTabs(next: TerminalTabsSnapshot): void {
  if (sameSnapshot(snapshot, next)) {
    return;
  }
  snapshot = next;
  listeners.forEach((listener) => listener());
}

export function resetWorkspaceTerminals(): void {
  restored = NOTHING_RESTORED;
  snapshot = { tabs: [], activeKey: null };
}
