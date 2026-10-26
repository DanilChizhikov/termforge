import { describe, expect, it } from 'vitest';
import { terminalTabsReducer, type TerminalTab } from './terminalTabs';

function addTab(): TerminalTab[] {
  return terminalTabsReducer([], { type: 'add', key: 't1', connectionId: 'c1' });
}

describe('terminalTabsReducer reconnect', () => {
  it('remembers that the tab was connected once', () => {
    let tabs = addTab();
    expect(tabs[0]!.wasConnected).toBe(false);

    tabs = terminalTabsReducer(tabs, { type: 'state', key: 't1', state: 'connected', reason: null });
    tabs = terminalTabsReducer(tabs, { type: 'state', key: 't1', state: 'disconnected', reason: 'lost' });

    expect(tabs[0]!.wasConnected).toBe(true);
    expect(tabs[0]!.state).toBe('disconnected');
  });

  it('restarts the tab with a new generation and forgets the old session', () => {
    let tabs = addTab();
    tabs = terminalTabsReducer(tabs, {
      type: 'opened',
      key: 't1',
      session: { id: 's1', connectionId: 'c1', connectionName: 'web', sessionNumber: 1, openedAt: '2026-10-26T10:00:00Z' },
    });
    tabs = terminalTabsReducer(tabs, { type: 'state', key: 't1', state: 'disconnected', reason: 'lost' });

    tabs = terminalTabsReducer(tabs, { type: 'reconnect', key: 't1' });

    expect(tabs[0]).toMatchObject({ generation: 1, reconnecting: true, state: 'connecting', session: null, reason: null });
  });

  it('only touches the requested tab', () => {
    let tabs = addTab();
    tabs = terminalTabsReducer(tabs, { type: 'add', key: 't2', connectionId: 'c2' });

    tabs = terminalTabsReducer(tabs, { type: 'reconnect', key: 't2' });

    expect(tabs[0]!.generation).toBe(0);
    expect(tabs[1]!.generation).toBe(1);
  });
});
