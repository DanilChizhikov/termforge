import { useReducer, useRef, useState } from 'react';
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
import { CloseIcon, ListIcon, PlusIcon } from './icons/Icons';
import { CommandHistoryPanel } from './CommandHistoryPanel';
import { ReconnectBar } from './ReconnectBar';
import { TerminalRecordingBar } from './TerminalRecordingBar';
import { TerminalView, type TerminalHandle } from './TerminalView';

const STATE_LABELS = {
  connecting: 'connecting',
  connected: 'connected',
  disconnected: 'disconnected',
} as const;

const STATE_DOTS = {
  connecting: 'status-dot-caution',
  connected: 'status-dot-ok',
  disconnected: 'status-dot-danger',
} as const;

function terminalTabClassName(tab: TerminalTab, active: boolean): string {
  const classNames = ['terminal-tab', `terminal-tab-${tab.state}`];
  if (active) {
    classNames.push('terminal-tab-active');
  }
  return classNames.join(' ');
}

export function TerminalWorkspace() {
  const selectedServerId = useSelectedServerId();
  const [tabs, dispatch] = useReducer(terminalTabsReducer, []);
  const [activeKey, setActiveKey] = useState<string | null>(null);
  const visibleKey = tabs.some((tab) => tab.key === activeKey) ? activeKey : (tabs[0]?.key ?? null);
  const [historyOpen, setHistoryOpen] = useState(true);
  const [historyVersion, setHistoryVersion] = useState(0);
  const [recordingKeys, setRecordingKeys] = useState<ReadonlySet<string>>(() => new Set());
  const handles = useRef(new Map<string, TerminalHandle>());
  const visibleTab = tabs.find((tab) => tab.key === visibleKey) ?? null;

  const registerHandle = (key: string) => (handle: TerminalHandle | null) => {
    if (handle === null) {
      handles.current.delete(key);
      return undefined;
    }
    handles.current.set(key, handle);
    return () => {
      handles.current.delete(key);
    };
  };

  const insertIntoActiveTerminal = (text: string) => {
    if (visibleKey !== null) {
      handles.current.get(visibleKey)?.insertText(text);
    }
  };

  const openTab = () => {
    if (selectedServerId === null) {
      return;
    }
    const key = createTabKey();
    dispatch({ type: 'add', key, connectionId: selectedServerId });
    setActiveKey(key);
  };

  const markRecording = (key: string, recording: boolean) => {
    setRecordingKeys((current) => {
      if (current.has(key) === recording) {
        return current;
      }
      const next = new Set(current);
      if (recording) {
        next.add(key);
      } else {
        next.delete(key);
      }
      return next;
    });
  };

  const closeTab = (tab: TerminalTab) => {
    if (tab.session !== null) {
      closeTerminal(tab.session.id).catch(() => undefined);
    }
    dispatch({ type: 'remove', key: tab.key });
  };

  return (
    <div className="terminal-workspace">
      <div className="terminal-bar">
        {tabs.length === 0 ? null : (
          <div aria-label="Terminal tabs" className="terminal-tab-groups">
            {groupTabsByServer(tabs).map((group) => (
              <div key={group.connectionId} role="group" aria-label={group.name} className="terminal-tab-group">
                <span className="terminal-group-label">{group.name}</span>
                <div role="tablist" aria-label={`${group.name} terminals`} className="terminal-tab-list">
                  {group.tabs.map((tab) => (
                    <span key={tab.key} className={terminalTabClassName(tab, tab.key === visibleKey)}>
                      <button
                        type="button"
                        role="tab"
                        id={`tab-${tab.key}`}
                        aria-selected={tab.key === visibleKey}
                        aria-controls={`panel-${tab.key}`}
                        className="terminal-tab-button"
                        onClick={() => setActiveKey(tab.key)}
                      >
                        <span className={`status-dot status-dot-sm ${STATE_DOTS[tab.state]}`} aria-hidden="true" />
                        {tabLabel(tab)}{' '}
                        <span className={tab.state === 'connected' ? 'visually-hidden' : 'terminal-tab-state'}>
                          ({STATE_LABELS[tab.state]})
                        </span>
                        {recordingKeys.has(tab.key) ? (
                          <span className="badge badge-recording">
                            <span className="status-dot status-dot-xs status-dot-active" aria-hidden="true" />
                            rec
                          </span>
                        ) : null}
                      </button>
                      <button
                        type="button"
                        className="icon-btn icon-btn-sm"
                        aria-label={`Close ${tabLabel(tab)}`}
                        onClick={() => closeTab(tab)}
                      >
                        <CloseIcon size={12} />
                      </button>
                    </span>
                  ))}
                </div>
              </div>
            ))}
          </div>
        )}
        {selectedServerId === null ? <span className="terminal-hint">Select a server to open a terminal.</span> : null}
        <span className="terminal-bar-spacer" />
        {historyOpen || visibleTab === null ? null : (
          <button type="button" className="btn btn-sm" onClick={() => setHistoryOpen(true)}>
            <ListIcon size={13} />
            History
          </button>
        )}
        <button
          type="button"
          className="btn btn-sm btn-filled"
          onClick={openTab}
          disabled={selectedServerId === null}
        >
          <PlusIcon size={13} />
          New terminal
        </button>
      </div>
      {tabs.length === 0 ? <EmptyState message="No terminal sessions. Connect to a server to open one." /> : null}
      <div className="terminal-body">
      <div className="terminal-stack">
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
            <ReconnectBar
              connectionId={tab.connectionId}
              reason={tab.reason}
              wasConnected={tab.wasConnected}
              onReconnect={() => dispatch({ type: 'reconnect', key: tab.key })}
            />
          ) : null}
          {tab.state === 'connecting' ? (
            <p className="terminal-connecting">{tab.reconnecting ? 'Reconnecting…' : 'Connecting…'}</p>
          ) : null}
          {tab.state === 'connected' && tab.session !== null ? (
            <TerminalRecordingBar
              key={tab.session.id}
              terminalId={tab.session.id}
              onRecordingChange={(recording) => markRecording(tab.key, recording)}
            />
          ) : null}
          <TerminalView
            connectionId={tab.connectionId}
            active={tab.key === visibleKey}
            generation={tab.generation}
            reconnect={tab.reconnecting}
            handleRef={registerHandle(tab.key)}
            onHistoryChanged={() => setHistoryVersion((version) => version + 1)}
            onOpened={(session) => dispatch({ type: 'opened', key: tab.key, session })}
            onStateChange={(state, reason) => dispatch({ type: 'state', key: tab.key, state, reason })}
          />
        </div>
      ))}
      </div>
      {historyOpen && visibleTab !== null ? (
        <CommandHistoryPanel
          connectionId={visibleTab.connectionId}
          connectionName={visibleTab.session?.connectionName ?? 'server'}
          version={historyVersion}
          onInsert={insertIntoActiveTerminal}
          onCollapse={() => setHistoryOpen(false)}
        />
      ) : null}
      </div>
    </div>
  );
}
