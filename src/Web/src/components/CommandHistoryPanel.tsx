import { useCallback, useEffect, useState } from 'react';
import {
  clearCommandHistory,
  deleteCommand,
  getShellIntegration,
  isInsertable,
  listCommandHistory,
  pinCommand,
  type CommandHistoryEntry,
  type ShellName,
} from '../api/commandHistory';
import { describeError } from '../api/connections';
import { ChevronRightIcon, InsertIcon, PinIcon, TrashIcon } from './icons/Icons';

export interface CommandHistoryPanelProps {
  connectionId: string;
  connectionName: string;
  version: number;
  onInsert: (text: string) => void;
  onCollapse: () => void;
}

const SHELLS: readonly ShellName[] = ['bash', 'zsh'];

function CommandRow({
  entry,
  onInsert,
  onTogglePin,
  onDelete,
}: {
  entry: CommandHistoryEntry;
  onInsert: (entry: CommandHistoryEntry) => void;
  onTogglePin: (entry: CommandHistoryEntry) => void;
  onDelete: (entry: CommandHistoryEntry) => void;
}) {
  const failed = entry.exitCode !== null && entry.exitCode !== 0;
  const insertable = isInsertable(entry);
  return (
    <li className="history-row">
      <code className="history-command" title={entry.command}>
        {entry.command}
      </code>
      {failed ? <span className="badge badge-danger">exit {entry.exitCode}</span> : null}
      <button
        type="button"
        className="icon-btn icon-btn-sm"
        aria-label={`Insert ${entry.command} into the terminal`}
        title={insertable ? 'Insert into the terminal. Press Enter there to run it.' : 'Multi-line commands cannot be inserted safely.'}
        disabled={!insertable}
        onClick={() => onInsert(entry)}
      >
        <InsertIcon size={13} />
      </button>
      <button
        type="button"
        className="icon-btn icon-btn-sm"
        aria-label={entry.isPinned ? `Unpin ${entry.command}` : `Pin ${entry.command}`}
        aria-pressed={entry.isPinned}
        onClick={() => onTogglePin(entry)}
      >
        <PinIcon size={13} />
      </button>
      <button
        type="button"
        className="icon-btn icon-btn-sm"
        aria-label={`Delete ${entry.command} from history`}
        onClick={() => onDelete(entry)}
      >
        <TrashIcon size={13} />
      </button>
    </li>
  );
}

export function CommandHistoryPanel({
  connectionId,
  connectionName,
  version,
  onInsert,
  onCollapse,
}: CommandHistoryPanelProps) {
  const [entries, setEntries] = useState<CommandHistoryEntry[]>([]);
  const [search, setSearch] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [reloads, setReloads] = useState(0);

  useEffect(() => {
    let cancelled = false;
    listCommandHistory(connectionId, search)
      .then((loaded) => {
        if (!cancelled) {
          setEntries(loaded);
          setError(null);
        }
      })
      .catch((failure: unknown) => {
        if (!cancelled) {
          setError(describeError(failure));
        }
      });
    return () => {
      cancelled = true;
    };
  }, [connectionId, search, version, reloads]);

  const reload = useCallback(() => setReloads((count) => count + 1), []);

  const run = (action: Promise<unknown>) => {
    action.then(reload).catch((failure: unknown) => setError(describeError(failure)));
  };

  const insertCommand = (entry: CommandHistoryEntry) => {
    setNotice('Inserted. Press Enter in the terminal to run it.');
    onInsert(entry.command);
  };

  const insertSnippet = (shell: ShellName) => {
    getShellIntegration(shell)
      .then((integration) => {
        onInsert(integration.snippet);
        setNotice('Snippet inserted. Press Enter in the terminal to start recording commands.');
      })
      .catch((failure: unknown) => setError(describeError(failure)));
  };

  const pinned = entries.filter((entry) => entry.isPinned);
  const recent = entries.filter((entry) => !entry.isPinned);

  return (
    <aside aria-label="Command history" className="history-panel">
      <div className="history-header">
        <span className="history-title">
          History <span className="text-muted">· {connectionName}</span>
        </span>
        <button type="button" className="icon-btn" aria-label="Collapse history" onClick={onCollapse}>
          <ChevronRightIcon size={14} />
        </button>
      </div>
      <input
        className="input input-sm"
        type="search"
        aria-label="Search history"
        placeholder="Search commands"
        value={search}
        onChange={(event) => setSearch(event.target.value)}
      />
      {error === null ? null : <p className="form-error">{error}</p>}
      {notice === null ? null : <p className="field-hint">{notice}</p>}
      {pinned.length > 0 ? (
        <section aria-label="Pinned commands" className="history-section">
          <h3 className="section-label">Pinned</h3>
          <ul className="history-list">
            {pinned.map((entry) => (
              <CommandRow
                key={entry.id}
                entry={entry}
                onInsert={insertCommand}
                onTogglePin={(item) => run(pinCommand(item.id, !item.isPinned))}
                onDelete={(item) => run(deleteCommand(item.id))}
              />
            ))}
          </ul>
        </section>
      ) : null}
      <section aria-label="Recent commands" className="history-section">
        <h3 className="section-label">Recent</h3>
        {recent.length === 0 ? (
          <p className="field-hint">
            {search.trim() === ''
              ? 'No commands recorded. Commands are captured only when the shell reports them (see below).'
              : 'No matching commands.'}
          </p>
        ) : (
          <ul className="history-list">
            {recent.map((entry) => (
              <CommandRow
                key={entry.id}
                entry={entry}
                onInsert={insertCommand}
                onTogglePin={(item) => run(pinCommand(item.id, !item.isPinned))}
                onDelete={(item) => run(deleteCommand(item.id))}
              />
            ))}
          </ul>
        )}
      </section>
      <div className="history-footer">
        <button
          type="button"
          className="btn btn-xs"
          disabled={recent.length === 0}
          onClick={() => run(clearCommandHistory(connectionId))}
        >
          Clear unpinned
        </button>
        <span className="field-hint">Record commands from this shell:</span>
        <div className="history-shells">
          {SHELLS.map((shell) => (
            <button key={shell} type="button" className="btn btn-xs" onClick={() => insertSnippet(shell)}>
              {shell}
            </button>
          ))}
        </div>
      </div>
    </aside>
  );
}
