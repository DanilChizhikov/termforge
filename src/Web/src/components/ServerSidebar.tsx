import { useCallback, useState } from 'react';
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
import { EmptyState } from './EmptyState';
import { ImportIcon, PlusIcon } from './icons/Icons';
import { ImportReport } from './ImportReport';
import { ServerForm } from './ServerForm';
import { ServerRow, type ServerStatus } from './ServerRow';
import { SidebarMetrics } from './SidebarMetrics';

type DialogState = { mode: 'add' } | { mode: 'edit'; connection: SavedConnection } | null;

const UNGROUPED = '';

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
      await reload();
    });

  const rename = (name: string) =>
    run(async () => {
      const newName = window.prompt(`Rename group "${name}" to:`, name);
      if (newName !== null && newName.trim() !== '' && newName !== name) {
        await renameGroup(name, newName);
        await reload();
      }
    });

  const ungroup = (name: string) =>
    run(async () => {
      await removeGroup(name);
      await reload();
    });

  const groups = groupConnections(connections);

  return (
    <div className="server-sidebar">
      <div className="server-toolbar">
        <button type="button" className="btn btn-sm btn-filled btn-block" onClick={() => setDialog({ mode: 'add' })}>
          <PlusIcon size={13} />
          Add server
        </button>
      </div>
      {message === null ? null : <p className="form-error">{message}</p>}
      {importResult === null ? null : (
        <ImportReport result={importResult} onDismiss={() => setImportResult(null)} />
      )}
      {connections.length === 0 ? <EmptyState message="No servers yet." /> : null}
      {[...groups.entries()].map(([group, items]) => (
        <section
          key={group}
          className="server-group"
          aria-label={group === UNGROUPED ? 'Ungrouped' : `Group ${group}`}
        >
          {group === UNGROUPED ? null : (
            <div className="group-header">
              <span className="group-name">{group}</span>
              <button
                type="button"
                className="btn btn-xs btn-ghost"
                aria-label={`Rename group ${group}`}
                onClick={() => void rename(group)}
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
            </div>
          )}
          <ul className="server-list">
            {items.map((connection) => (
              <ServerRow
                key={connection.id}
                connection={connection}
                status={statuses[connection.id] ?? null}
                selected={connection.id === selectedId}
                onSelect={() => selectServer(connection.id)}
                onTest={() => void test(connection)}
                onEdit={() => setDialog({ mode: 'edit', connection })}
                onRemove={() => void remove(connection)}
              />
            ))}
          </ul>
        </section>
      ))}
      {selectedId !== null && connections.some((connection) => connection.id === selectedId) ? (
        <SidebarMetrics serverId={selectedId} />
      ) : null}
      <div className="server-import">
        <button type="button" className="btn btn-sm btn-filled btn-block" onClick={() => void importConfig()}>
          <ImportIcon size={13} />
          Import SSH config
        </button>
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
