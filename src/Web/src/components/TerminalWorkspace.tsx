import { useReducer, useState } from 'react';
import { closeTerminal } from '../api/terminals';
import { useSelectedServerId } from '../state/selectedServer';
import {
  createTabKey,
  groupTabsByServer,
  tabLabel,
  terminalTabsReducer,
  type TerminalTab,
} from '../state/terminalTabs';
import { EmptyState } from './EmptyState';
import { TerminalView } from './TerminalView';

const STATE_LABELS = {
  connecting: 'connecting',
  connected: 'connected',
  disconnected: 'disconnected',
} as const;

export function TerminalWorkspace() {
  const selectedServerId = useSelectedServerId();
  const [tabs, dispatch] = useReducer(terminalTabsReducer, []);
  const [activeKey, setActiveKey] = useState<string | null>(null);
  const visibleKey = tabs.some((tab) => tab.key === activeKey) ? activeKey : (tabs[0]?.key ?? null);

  const openTab = () => {
    if (selectedServerId === null) {
      return;
    }
    const key = createTabKey();
    dispatch({ type: 'add', key, connectionId: selectedServerId });
    setActiveKey(key);
  };

  const closeTab = (tab: TerminalTab) => {
    if (tab.session !== null) {
      closeTerminal(tab.session.id).catch(() => undefined);
    }
    dispatch({ type: 'remove', key: tab.key });
  };

  return (
    <div className="terminal-workspace">
      <div className="terminal-toolbar">
        <button type="button" onClick={openTab} disabled={selectedServerId === null}>
          New terminal
        </button>
        {selectedServerId === null ? <span>Select a server to open a terminal.</span> : null}
      </div>
      {tabs.length === 0 ? (
        <EmptyState message="No terminal sessions. Connect to a server to open one." />
      ) : (
        <div aria-label="Terminal tabs" className="terminal-tab-groups">
          {groupTabsByServer(tabs).map((group) => (
            <div key={group.connectionId} role="group" aria-label={group.name} className="terminal-tab-group">
              <span className="terminal-group-label">{group.name}:</span>
              <div role="tablist" aria-label={`${group.name} terminals`} className="tab-strip">
                {group.tabs.map((tab) => (
                  <span key={tab.key} className="terminal-tab">
                    <button
                      type="button"
                      role="tab"
                      id={`tab-${tab.key}`}
                      aria-selected={tab.key === visibleKey}
                      aria-controls={`panel-${tab.key}`}
                      className={tab.key === visibleKey ? 'tab tab-active' : 'tab'}
                      onClick={() => setActiveKey(tab.key)}
                    >
                      {tabLabel(tab)} ({STATE_LABELS[tab.state]})
                    </button>
                    <button type="button" aria-label={`Close ${tabLabel(tab)}`} onClick={() => closeTab(tab)}>
                      x
                    </button>
                  </span>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
      {tabs.map((tab) => (
        <div
          key={tab.key}
          role="tabpanel"
          id={`panel-${tab.key}`}
          aria-labelledby={`tab-${tab.key}`}
          hidden={tab.key !== visibleKey}
          className="terminal-panel"
        >
          {tab.state === 'disconnected' ? (
            <p role="status" className="terminal-disconnected">
              Disconnected: {tab.reason}
            </p>
          ) : null}
          {tab.state === 'connecting' ? <p className="terminal-connecting">Connecting…</p> : null}
          <TerminalView
            connectionId={tab.connectionId}
            active={tab.key === visibleKey}
            onOpened={(session) => dispatch({ type: 'opened', key: tab.key, session })}
            onStateChange={(state, reason) => dispatch({ type: 'state', key: tab.key, state, reason })}
          />
        </div>
      ))}
    </div>
  );
}
