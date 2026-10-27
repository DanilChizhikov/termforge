import { useCallback, useState, useSyncExternalStore, type FormEvent } from 'react';
import {
  createConnection,
  describeError,
  emptyInput,
  importSshConfig,
  inputFrom,
  removeConnection,
  removeGroup,
  renameGroup,
  testConnection,
  updateConnection,
  type ConnectionInput,
  type ConnectionTestResult,
  type SavedConnection,
  type SshConfigImportResult,
} from '../api/connections';
import { reloadConnections, useConnections } from '../state/connections';
import { selectServer, useSelectedServerId } from '../state/selectedServer';
import { getTerminalTabsSnapshot, subscribeTerminalTabs } from '../state/workspaceTerminals';
import { RegionToggle } from './CollapsibleRegion';
import { EmptyState } from './EmptyState';
import { ChevronDownIcon, ChevronRightIcon, ImportIcon, PlusIcon } from './icons/Icons';
import { ImportReport } from './ImportReport';
import { ServerForm } from './ServerForm';
import { ServerRow, type ServerStatus } from './ServerRow';
import { formatClock } from './timeFormat';

type DialogState = { mode: 'add' } | { mode: 'edit'; connection: SavedConnection } | null;

const UNGROUPED = '';

interface ImportSummary {
  hosts: number;
  at: Date;
}

function useSessionCounts(): Map<string, number> {
  const snapshot = useSyncExternalStore(subscribeTerminalTabs, getTerminalTabsSnapshot, getTerminalTabsSnapshot);
  const counts = new Map<string, number>();
  snapshot.tabs.forEach((tab) => counts.set(tab.connectionId, (counts.get(tab.connectionId) ?? 0) + 1));
  return counts;
}

function groupConnections(connections: readonly SavedConnection[]): Map<string, SavedConnection[]> {
  const groups = new Map<string, SavedConnection[]>();
  for (const connection of connections) {
    const key = connection.groupName ?? UNGROUPED;
    groups.set(key, [...(groups.get(key) ?? []), connection]);
  }
  return new Map([...groups.entries()].sort(([left], [right]) => left.localeCompare(right)));
}

export function ServerSidebar() {
  const selectedId = useSelectedServerId();
  const connections = useConnections();
  const [statuses, setStatuses] = useState<Record<string, ServerStatus>>({});
  const [dialog, setDialog] = useState<DialogState>(null);
  const [importResult, setImportResult] = useState<SshConfigImportResult | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [lastImport, setLastImport] = useState<ImportSummary | null>(null);
  const [collapsedGroups, setCollapsedGroups] = useState<ReadonlySet<string>>(() => new Set());
  const [renamingGroup, setRenamingGroup] = useState<string | null>(null);
  const sessionCounts = useSessionCounts();

  const reload = useCallback(async () => {
    const list = await reloadConnections();
    if (selectedId !== null && !list.some((connection) => connection.id === selectedId)) {
      selectServer(null);
    }
  }, [selectedId]);

  const run = async (action: () => Promise<void>) => {
    setMessage(null);
    try {
      await action();
    } catch (failure) {
      setMessage(describeError(failure));
    }
  };

  const save = async (input: ConnectionInput) => {
    if (dialog?.mode === 'edit') {
      await updateConnection(dialog.connection.id, input);
    } else {
      await createConnection(input);
    }
    setDialog(null);
    await reload();
  };

  const test = (connection: SavedConnection) =>
    run(async () => {
      setStatuses((current) => ({ ...current, [connection.id]: 'testing' }));
      try {
        const result: ConnectionTestResult = await testConnection(connection.id);
        setStatuses((current) => ({ ...current, [connection.id]: result }));
        await reload();
      } catch (failure) {
        setStatuses((current) => {
          const rest = { ...current };
          delete rest[connection.id];
          return rest;
        });
        throw failure;
      }
    });

  const remove = (connection: SavedConnection) =>
    run(async () => {
      await removeConnection(connection.id);
      await reload();
    });

  const importConfig = () =>
    run(async () => {
      const result = await importSshConfig();
      setImportResult(result);
      setLastImport({ hosts: result.created + result.updated, at: new Date() });
      await reload();
    });

  const rename = (name: string, newName: string) =>
    run(async () => {
      setRenamingGroup(null);
      const trimmed = newName.trim();
      if (trimmed !== '' && trimmed !== name) {
        await renameGroup(name, trimmed);
        await reload();
      }
    });

  const ungroup = (name: string) =>
    run(async () => {
      await removeGroup(name);
      await reload();
    });

  const toggleGroup = (name: string) => {
    setCollapsedGroups((current) => {
      const next = new Set(current);
      if (next.has(name)) {
        next.delete(name);
      } else {
        next.add(name);
      }
      return next;
    });
  };

  const submitRename = (event: FormEvent<HTMLFormElement>, name: string) => {
    event.preventDefault();
    const value = new FormData(event.currentTarget).get('groupName');
    void rename(name, typeof value === 'string' ? value : name);
  };

  const groups = groupConnections(connections);

  return (
    <div className="server-sidebar">
      <div className="sidebar-header">
        <span className="section-label">Servers</span>
        <span className="sidebar-header-actions">
          <button type="button" className="icon-btn" aria-label="Add server" onClick={() => setDialog({ mode: 'add' })}>
            <PlusIcon size={16} />
          </button>
          <RegionToggle />
        </span>
      </div>
      {message === null ? null : <p className="form-error">{message}</p>}
      {importResult === null ? null : (
        <ImportReport result={importResult} onDismiss={() => setImportResult(null)} />
      )}
      {connections.length === 0 ? <EmptyState message="No servers yet." /> : null}
      {[...groups.entries()].map(([group, items]) => {
        const groupCollapsed = collapsedGroups.has(group);
        return (
          <section
            key={group}
            className="server-group"
            aria-label={group === UNGROUPED ? 'Ungrouped' : `Group ${group}`}
          >
            {group === UNGROUPED ? null : (
              <div className="group-header">
                {renamingGroup === group ? (
                  <form className="group-rename" onSubmit={(event) => submitRename(event, group)}>
                    <input
                      className="input input-sm"
                      name="groupName"
                      aria-label={`New name for group ${group}`}
                      defaultValue={group}
                      autoFocus
                      onKeyDown={(event) => {
                        if (event.key === 'Escape') {
                          setRenamingGroup(null);
                        }
                      }}
                    />
                    <button type="submit" className="btn btn-xs">
                      Save
                    </button>
                  </form>
                ) : (
                  <>
                    <button
                      type="button"
                      className="group-toggle"
                      aria-expanded={!groupCollapsed}
                      onClick={() => toggleGroup(group)}
                    >
                      {groupCollapsed ? <ChevronRightIcon size={12} /> : <ChevronDownIcon size={12} />}
                      <span className="group-name">{group}</span>
                    </button>
                    <span className="group-actions">
                      <button
                        type="button"
                        className="btn btn-xs btn-ghost"
                        aria-label={`Rename group ${group}`}
                        onClick={() => setRenamingGroup(group)}
                      >
                        Rename
                      </button>
                      <button
                        type="button"
                        className="btn btn-xs btn-ghost"
                        aria-label={`Ungroup ${group}`}
                        onClick={() => void ungroup(group)}
                      >
                        Ungroup
                      </button>
                    </span>
                  </>
                )}
              </div>
            )}
            {groupCollapsed ? null : (
              <ul className="server-list">
                {items.map((connection) => (
                  <ServerRow
                    key={connection.id}
                    connection={connection}
                    status={statuses[connection.id] ?? null}
                    sessionCount={sessionCounts.get(connection.id) ?? 0}
                    selected={connection.id === selectedId}
                    onSelect={() => selectServer(connection.id)}
                    onTest={() => void test(connection)}
                    onEdit={() => setDialog({ mode: 'edit', connection })}
                    onRemove={() => void remove(connection)}
                  />
                ))}
              </ul>
            )}
          </section>
        );
      })}
      <div className="server-import">
        <button type="button" className="btn btn-filled btn-block server-import-button" onClick={() => void importConfig()}>
          <ImportIcon size={14} />
          Import <code>~/.ssh/config</code>
        </button>
        {lastImport === null ? null : (
          <span className="server-import-caption">
            {lastImport.hosts === 1 ? '1 host' : `${lastImport.hosts} hosts`} · imported {formatClock(lastImport.at)}
          </span>
        )}
      </div>
      {dialog === null ? null : (
        <ServerForm
          title={dialog.mode === 'edit' ? 'Edit server' : 'Add server'}
          initial={dialog.mode === 'edit' ? inputFrom(dialog.connection) : emptyInput()}
          editingId={dialog.mode === 'edit' ? dialog.connection.id : null}
          connections={connections}
          hasStoredPassword={dialog.mode === 'edit' && dialog.connection.hasPassword}
          onSubmit={save}
          onCancel={() => setDialog(null)}
        />
      )}
    </div>
  );
}
