import type { TerminalSession, TerminalState } from '../api/terminals';

export interface TerminalTab {
  key: string;
  connectionId: string;
  session: TerminalSession | null;
  state: TerminalState;
  reason: string | null;
  generation: number;
  reconnecting: boolean;
  wasConnected: boolean;
  connectionName: string | null;
  restored: boolean;
}

export type TerminalTabsAction =
  | { type: 'add'; key: string; connectionId: string }
  | { type: 'opened'; key: string; session: TerminalSession }
  | { type: 'state'; key: string; state: TerminalState; reason: string | null }
  | { type: 'reconnect'; key: string }
  | { type: 'resume'; key: string }
  | { type: 'remove'; key: string };

export interface TerminalTabGroup {
  connectionId: string;
  name: string;
  tabs: TerminalTab[];
}

const CONNECTING_LABEL = 'Connecting…';
const UNNAMED_LABEL = 'Terminal';
const RESTORED_REASON = 'Restored from your last session.';

let tabCounter = 0;

export function createTabKey(): string {
  tabCounter += 1;
  return `terminal-${tabCounter}`;
}

export function restoredTerminalTab(key: string, connectionId: string, connectionName: string): TerminalTab {
  return {
    key,
    connectionId,
    session: null,
    state: 'disconnected',
    reason: RESTORED_REASON,
    generation: 0,
    reconnecting: false,
    wasConnected: false,
    connectionName: connectionName === '' ? null : connectionName,
    restored: true,
  };
}

export function terminalTabsReducer(tabs: TerminalTab[], action: TerminalTabsAction): TerminalTab[] {
  switch (action.type) {
    case 'add':
      return [
        ...tabs,
        {
          key: action.key,
          connectionId: action.connectionId,
          session: null,
          state: 'connecting',
          reason: null,
          generation: 0,
          reconnecting: false,
          wasConnected: false,
          connectionName: null,
          restored: false,
        },
      ];
    case 'opened':
      return tabs.map((tab) =>
        tab.key === action.key ? { ...tab, session: action.session, connectionName: action.session.connectionName } : tab,
      );
    case 'state':
      return tabs.map((tab) =>
        tab.key === action.key
          ? {
              ...tab,
              state: action.state,
              reason: action.reason,
              wasConnected: tab.wasConnected || action.state === 'connected',
            }
          : tab,
      );
    case 'reconnect':
      return tabs.map((tab) =>
        tab.key === action.key
          ? { ...tab, session: null, state: 'connecting', reason: null, generation: tab.generation + 1, reconnecting: true }
          : tab,
      );
    case 'resume':
      return tabs.map((tab) =>
        tab.key === action.key && tab.restored
          ? { ...tab, restored: false, state: 'connecting', reason: null, generation: tab.generation + 1 }
          : tab,
      );
    case 'remove':
      return tabs.filter((tab) => tab.key !== action.key);
  }
}

export function groupTabsByServer(tabs: TerminalTab[]): TerminalTabGroup[] {
  const groups = new Map<string, TerminalTabGroup>();
  for (const tab of tabs) {
    const group = groups.get(tab.connectionId) ?? { connectionId: tab.connectionId, name: CONNECTING_LABEL, tabs: [] };
    group.tabs.push(tab);
    if (tab.session !== null) {
      group.name = tab.session.connectionName;
    } else if (tab.connectionName !== null) {
      group.name = tab.connectionName;
    }
    groups.set(tab.connectionId, group);
  }
  return [...groups.values()];
}

export function tabLabel(tab: TerminalTab): string {
  if (tab.session === null) {
    return tab.restored ? (tab.connectionName ?? UNNAMED_LABEL) : CONNECTING_LABEL;
  }
  return `${tab.session.connectionName} #${tab.session.sessionNumber}`;
}
