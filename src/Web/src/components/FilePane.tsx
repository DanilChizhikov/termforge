import { useEffect, useMemo, useState, type FormEvent } from 'react';
import {
  describeFileError,
  isNavigable,
  sortEntries,
  type DirectoryListing,
  type FileEntry,
  type FileSource,
  type LocalRoot,
  type SortDirection,
  type SortKey,
} from '../api/files';
import { EmptyState } from './EmptyState';
import { FileBreadcrumbs } from './FileBreadcrumbs';
import { FileTable } from './FileTable';
import { ArrowUpIcon, PlusIcon, RefreshIcon } from './icons/Icons';

export interface FilePaneProps {
  title: string;
  sourceKey: string;
  source: FileSource;
  path: string | null;
  onPathChange: (path: string) => void;
  roots?: LocalRoot[];
}

interface LoadState {
  key: string;
  listing: DirectoryListing | null;
  error: string | null;
}

interface Selection {
  key: string;
  paths: ReadonlySet<string>;
}

type PaneAction =
  | { type: 'createDirectory' }
  | { type: 'rename'; entry: FileEntry }
  | { type: 'delete'; entries: FileEntry[] };

const NO_SELECTION: ReadonlySet<string> = new Set();

export function FilePane({ title, sourceKey, source, path, onPathChange, roots }: FilePaneProps) {
  const [loadState, setLoadState] = useState<LoadState | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [selection, setSelection] = useState<Selection | null>(null);
  const [sortKey, setSortKey] = useState<SortKey>('name');
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc');
  const [action, setAction] = useState<PaneAction | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const requestKey = `${sourceKey}|${path ?? ''}|${reloadToken}`;

  useEffect(() => {
    let cancelled = false;
    source
      .list(path)
      .then((listing) => {
        if (cancelled) {
          return;
        }
        setLoadState({ key: requestKey, listing, error: null });
        if (listing.path !== path) {
          onPathChange(listing.path);
        }
      })
      .catch((error: unknown) => {
        if (!cancelled) {
          setLoadState({ key: requestKey, listing: null, error: describeFileError(error) });
        }
      });
    return () => {
      cancelled = true;
    };
  }, [source, path, requestKey, onPathChange]);

  const loading = loadState === null || loadState.key !== requestKey;
  const listing = loading ? null : loadState.listing;
  const loadError = loading ? null : loadState.error;
  const currentPath = listing?.path ?? path;
  const selectedPaths = selection !== null && selection.key === requestKey ? selection.paths : NO_SELECTION;

  const entries = useMemo(
    () => (listing === null ? [] : sortEntries(listing.entries, sortKey, sortDirection)),
    [listing, sortKey, sortDirection],
  );
  const selectedEntries = entries.filter((entry) => selectedPaths.has(entry.path));

  function navigate(target: string): void {
    setAction(null);
    setActionError(null);
    onPathChange(target);
  }

  function refresh(): void {
    setReloadToken((token) => token + 1);
  }

  function handleSort(key: SortKey): void {
    if (key === sortKey) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
      return;
    }
    setSortKey(key);
    setSortDirection('asc');
  }

  function handleSelect(entry: FileEntry, additive: boolean): void {
    const next = new Set(additive ? selectedPaths : []);
    if (additive && next.has(entry.path)) {
      next.delete(entry.path);
    } else {
      next.add(entry.path);
    }
    setSelection({ key: requestKey, paths: next });
  }

  function handleOpen(entry: FileEntry): void {
    if (isNavigable(entry)) {
      navigate(entry.path);
    }
  }

  function openAction(next: PaneAction): void {
    setActionError(null);
    setAction(next);
  }

  function openRename(candidates: FileEntry[]): void {
    const [entry] = candidates;
    if (entry !== undefined) {
      openAction({ type: 'rename', entry });
    }
  }

  async function runAction(operation: () => Promise<void>): Promise<void> {
    setBusy(true);
    setActionError(null);
    try {
      await operation();
      setAction(null);
      refresh();
    } catch (error) {
      setActionError(describeFileError(error));
    } finally {
      setBusy(false);
    }
  }

  async function submitName(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    if (action === null || action.type === 'delete' || currentPath === null) {
      return;
    }
    const data = new FormData(event.currentTarget);
    const name = String(data.get('name') ?? '').trim();
    const parent = currentPath;
    if (action.type === 'createDirectory') {
      await runAction(() => source.createDirectory(parent, name));
      return;
    }
    const target = action.entry;
    await runAction(() => source.rename(target.path, name));
  }

  async function confirmDelete(): Promise<void> {
    if (action === null || action.type !== 'delete') {
      return;
    }
    const targets = action.entries;
    await runAction(async () => {
      for (const entry of targets) {
        await source.remove(entry.path);
      }
    });
  }

  return (
    <section className="file-pane" aria-label={`${title} files`}>
      <div className="file-toolbar">
        {roots !== undefined && roots.length > 0 && (
          <select
            className="input input-sm"
            aria-label={`${title} roots`}
            value=""
            onChange={(event) => {
              if (event.target.value !== '') {
                navigate(event.target.value);
              }
            }}
          >
            <option value="">Roots...</option>
            {roots.map((root) => (
              <option key={root.path} value={root.path}>
                {root.name}
              </option>
            ))}
          </select>
        )}
        <button
          type="button"
          className="btn btn-sm btn-ghost"
          disabled={listing === null || listing.parentPath === null}
          onClick={() => listing?.parentPath != null && navigate(listing.parentPath)}
        >
          <ArrowUpIcon size={13} />
          Up
        </button>
        <button type="button" className="btn btn-sm btn-ghost" onClick={refresh}>
          <RefreshIcon size={13} />
          Refresh
        </button>
        <button
          type="button"
          className="btn btn-sm btn-ghost"
          disabled={listing === null || busy}
          onClick={() => openAction({ type: 'createDirectory' })}
        >
          <PlusIcon size={13} />
          New folder
        </button>
        <button
          type="button"
          className="btn btn-sm btn-ghost"
          disabled={selectedEntries.length !== 1 || busy}
          onClick={() => openRename(selectedEntries)}
        >
          Rename
        </button>
        <button
          type="button"
          className="btn btn-sm btn-ghost btn-danger-link"
          disabled={selectedEntries.length === 0 || busy}
          onClick={() => openAction({ type: 'delete', entries: selectedEntries })}
        >
          Delete
        </button>
      </div>
      {currentPath !== null && <FileBreadcrumbs path={currentPath} onNavigate={navigate} />}
      {loadError !== null && (
        <p role="status" className="form-error">
          {loadError}
        </p>
      )}
      {actionError !== null && (
        <p role="alert" className="form-error">
          {actionError}
        </p>
      )}
      {action !== null && action.type !== 'delete' && (
        <form
          key={action.type === 'rename' ? action.entry.path : 'create'}
          aria-label={action.type === 'rename' ? 'Rename entry' : 'Create folder'}
          className="file-action"
          onSubmit={(event) => void submitName(event)}
        >
          <label className="field">
            <span className="field-label">{action.type === 'rename' ? 'New name' : 'Folder name'}</span>
            <input
              className="input input-sm input-mono"
              name="name"
              defaultValue={action.type === 'rename' ? action.entry.name : ''}
              autoFocus
            />
          </label>
          <div className="file-action-buttons">
            <button type="button" className="btn btn-sm" onClick={() => setAction(null)}>
              Cancel
            </button>
            <button type="submit" className="btn btn-sm btn-primary" disabled={busy}>
              {action.type === 'rename' ? 'Rename' : 'Create'}
            </button>
          </div>
        </form>
      )}
      {action !== null && action.type === 'delete' && (
        <div role="alertdialog" aria-label="Confirm delete" className="file-action file-action-danger">
          <p>
            Delete {action.entries.length} item(s)? Only files and empty folders can be deleted.
          </p>
          <ul>
            {action.entries.map((entry) => (
              <li key={entry.path}>{entry.name}</li>
            ))}
          </ul>
          <div className="file-action-buttons">
            <button type="button" className="btn btn-sm btn-safe" onClick={() => setAction(null)}>
              Cancel
            </button>
            <button
              type="button"
              className="btn btn-sm btn-destructive"
              disabled={busy}
              onClick={() => void confirmDelete()}
            >
              Confirm delete
            </button>
          </div>
        </div>
      )}
      {loading && <EmptyState message="Loading..." />}
      {listing !== null && entries.length === 0 && <EmptyState message="This folder is empty." />}
      {listing !== null && entries.length > 0 && (
        <div className="file-table-scroll">
          <FileTable
            entries={entries}
            selected={selectedPaths}
            sortKey={sortKey}
            sortDirection={sortDirection}
            onSort={handleSort}
            onSelect={handleSelect}
            onOpen={handleOpen}
          />
        </div>
      )}
    </section>
  );
}
