import { useEffect, useReducer, useRef, useState } from 'react';
import { closeTerminal, type TerminalSize } from '../api/terminals';
import { useConnectionName } from '../state/connections';
import { useSelectedServerId } from '../state/selectedServer';
import {
  createTabKey,
  groupTabsByServer,
  tabLabel,
  terminalTabsReducer,
  type TerminalTab,
} from '../state/terminalTabs';
import { getRestoredTerminals, markRestoredTerminalsUsed, publishTerminalTabs } from '../state/workspaceTerminals';
import { EmptyState } from './EmptyState';
import { CloseIcon, ListIcon, PlusIcon, RefreshIcon } from './icons/Icons';
import { CommandHistoryPanel } from './CommandHistoryPanel';
import { RegionToggle } from './CollapsibleRegion';
import { ReconnectBar } from './ReconnectBar';
import { TerminalMetaBar } from './TerminalMetaBar';
import { TerminalView, type TerminalHandle } from './TerminalView';

const STATE_LABELS = {
  connecting: 'connecting',
  connected: 'connected',
  disconnected: 'disconnected',
} as const;

const STATE_DOTS = {
  connecting: 'status-dot-active',
  connected: 'status-dot-ok',
  disconnected: 'status-dot-danger',
} as const;

const RESTORED_LABEL = 'restored';
const RESTORED_DOT = 'status-dot-idle';

function stateLabel(tab: TerminalTab): string {
  return tab.restored ? RESTORED_LABEL : STATE_LABELS[tab.state];
}

function stateDot(tab: TerminalTab): string {
  if (tab.restored) {
    return RESTORED_DOT;
  }
  return tab.reconnecting && tab.state === 'connecting' ? 'status-dot-caution' : STATE_DOTS[tab.state];
}

function isReconnecting(tab: TerminalTab): boolean {
  return tab.reconnecting && tab.state === 'connecting';
}

function terminalTabClassName(tab: TerminalTab, active: boolean): string {
  const classNames = ['terminal-tab', tab.restored ? 'terminal-tab-restored' : `terminal-tab-${tab.state}`];
  if (isReconnecting(tab)) {
    classNames.push('terminal-tab-reconnecting');
  }
  if (active) {
    classNames.push('terminal-tab-active');
  }
  return classNames.join(' ');
}

export function TerminalWorkspace() {
  const selectedServerId = useSelectedServerId();
  const [restored] = useState(getRestoredTerminals);
  const [tabs, dispatch] = useReducer(terminalTabsReducer, restored.tabs);
  const [activeKey, setActiveKey] = useState<string | null>(restored.activeKey);
  const visibleKey = tabs.some((tab) => tab.key === activeKey) ? activeKey : (tabs[0]?.key ?? null);
  const [historyOpen, setHistoryOpen] = useState(true);
  const [historyVersion, setHistoryVersion] = useState(0);
  const [recordingKeys, setRecordingKeys] = useState<ReadonlySet<string>>(() => new Set());
  const [sizes, setSizes] = useState<ReadonlyMap<string, TerminalSize>>(() => new Map());
  const selectedServerName = useConnectionName(selectedServerId);
  const handles = useRef(new Map<string, TerminalHandle>());
  const visibleTab = tabs.find((tab) => tab.key === visibleKey) ?? null;

  useEffect(() => {
    markRestoredTerminalsUsed();
  }, []);

  useEffect(() => {
    const persisted = tabs.map((tab) => ({
      key: tab.key,
      connectionId: tab.connectionId,
      connectionName: tab.session?.connectionName ?? tab.connectionName ?? '',
    }));
    publishTerminalTabs({ tabs: persisted, activeKey: visibleKey });
  }, [tabs, visibleKey]);

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

  const recordSize = (key: string, size: TerminalSize) => {
    setSizes((current) => {
      const previous = current.get(key);
      if (previous !== undefined && previous.columns === size.columns && previous.rows === size.rows) {
        return current;
      }
      const next = new Map(current);
      next.set(key, size);
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
                        <span className={`status-dot status-dot-sm ${stateDot(tab)}`} aria-hidden="true" />
                        {tabLabel(tab)}
                        {isReconnecting(tab) ? ' · reconnecting' : null}{' '}
                        <span className="visually-hidden">({stateLabel(tab)})</span>
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
          {selectedServerName === null ? 'New session' : `New session on ${selectedServerName}`}
        </button>
        <RegionToggle />
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
          {tab.restored ? (
            <p role="status" className="terminal-restored">
              <span>Restored from your last session. The previous shell was not kept.</span>
              <button type="button" className="btn btn-sm" onClick={() => dispatch({ type: 'resume', key: tab.key })}>
                <RefreshIcon size={13} />
                Reconnect
              </button>
            </p>
          ) : null}
          {tab.state === 'disconnected' && !tab.restored ? (
            <ReconnectBar
              connectionId={tab.connectionId}
              reason={tab.reason}
              wasConnected={tab.wasConnected}
              onReconnect={() => dispatch({ type: 'reconnect', key: tab.key })}
            />
          ) : null}
          {tab.state === 'connecting' ? (
            <p className={tab.reconnecting ? 'terminal-connecting terminal-connecting-retry' : 'terminal-connecting'}>
              <span
                className={tab.reconnecting ? 'status-dot status-dot-sm status-dot-caution' : 'status-dot status-dot-sm status-dot-active'}
                aria-hidden="true"
              />
              {tab.reconnecting ? 'Reconnecting…' : 'Connecting…'}
            </p>
          ) : null}
          {tab.state === 'connected' && tab.session !== null ? (
            <TerminalMetaBar
              session={tab.session}
              size={sizes.get(tab.key) ?? null}
              onRecordingChange={(recording) => markRecording(tab.key, recording)}
            />
          ) : null}
          {tab.restored ? null : (
          <TerminalView
            connectionId={tab.connectionId}
            active={tab.key === visibleKey}
            generation={tab.generation}
            reconnect={tab.reconnecting}
            handleRef={registerHandle(tab.key)}
            onHistoryChanged={() => setHistoryVersion((version) => version + 1)}
            onResize={(size) => recordSize(tab.key, size)}
            onOpened={(session) => dispatch({ type: 'opened', key: tab.key, session })}
            onStateChange={(state, reason) => dispatch({ type: 'state', key: tab.key, state, reason })}
          />
          )}
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
