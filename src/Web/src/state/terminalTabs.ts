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
}

export type TerminalTabsAction =
  | { type: 'add'; key: string; connectionId: string }
  | { type: 'opened'; key: string; session: TerminalSession }
  | { type: 'state'; key: string; state: TerminalState; reason: string | null }
  | { type: 'reconnect'; key: string }
  | { type: 'remove'; key: string };

export interface TerminalTabGroup {
  connectionId: string;
  name: string;
  tabs: TerminalTab[];
}

const CONNECTING_LABEL = 'Connecting…';

let tabCounter = 0;

export function createTabKey(): string {
  tabCounter += 1;
  return `terminal-${tabCounter}`;
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
        },
      ];
    case 'opened':
      return tabs.map((tab) => (tab.key === action.key ? { ...tab, session: action.session } : tab));
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
    }
    groups.set(tab.connectionId, group);
  }
  return [...groups.values()];
}

export function tabLabel(tab: TerminalTab): string {
  if (tab.session === null) {
    return CONNECTING_LABEL;
  }
  return `${tab.session.connectionName} #${tab.session.sessionNumber}`;
}
