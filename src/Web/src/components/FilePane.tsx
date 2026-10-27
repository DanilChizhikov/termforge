import { useEffect, useMemo, useState, type DragEvent, type FormEvent } from 'react';
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
import type { FileSearchMatch, FileSearchSource } from '../api/fileSearch';
import { RegionToggle } from './CollapsibleRegion';
import { EmptyState } from './EmptyState';
import { FileBreadcrumbs } from './FileBreadcrumbs';
import { FileSearchPanel } from './FileSearchPanel';
import { FileTable } from './FileTable';
import { ArrowUpIcon, FileIcon, PlusIcon, RefreshIcon, SearchIcon } from './icons/Icons';
import { hasDraggedPaths, readDraggedPaths, writeDraggedPaths, type FilePaneTransfer } from './filePaneTransfer';

export interface FilePaneProps {
  title: string;
  sourceKey: string;
  source: FileSource;
  path: string | null;
  onPathChange: (path: string) => void;
  roots?: LocalRoot[];
  onOpenFile?: (entry: FileEntry) => void;
  transfer?: FilePaneTransfer;
  refreshToken?: number;
  search?: FileSearchSource;
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

export function FilePane({ title, sourceKey, source, path, onPathChange, roots, onOpenFile, transfer, refreshToken = 0, search }: FilePaneProps) {
  const [loadState, setLoadState] = useState<LoadState | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [selection, setSelection] = useState<Selection | null>(null);
  const [sortKey, setSortKey] = useState<SortKey>('name');
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc');
  const [action, setAction] = useState<PaneAction | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [dropActive, setDropActive] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);

  const requestKey = `${sourceKey}|${path ?? ''}|${reloadToken}|${refreshToken}`;

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
  const showSearch = searchOpen && search !== undefined && currentPath !== null;

  function navigate(target: string): void {
    setAction(null);
    setActionError(null);
    onPathChange(target);
  }

  function openSearchLocation(match: FileSearchMatch): void {
    const locationKey = `${sourceKey}|${match.parentPath}|${reloadToken}|${refreshToken}`;
    setSelection({ key: locationKey, paths: new Set([match.entry.path]) });
    setSearchOpen(false);
    navigate(match.parentPath);
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
      return;
    }
    if (entry.kind === 'File') {
      onOpenFile?.(entry);
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

  function handleDragStart(entry: FileEntry, event: DragEvent<HTMLElement>): void {
    if (transfer === undefined) {
      return;
    }
    const paths = selectedPaths.has(entry.path) ? selectedEntries.map((item) => item.path) : [entry.path];
    writeDraggedPaths(event.dataTransfer, { origin: transfer.dragOrigin, paths });
  }

  function handleDragOver(event: DragEvent<HTMLElement>): void {
    if (transfer?.onReceive === undefined || !transfer.canReceive || !hasDraggedPaths(event.dataTransfer, transfer.acceptsFrom)) {
      return;
    }
    event.preventDefault();
    event.dataTransfer.dropEffect = 'copy';
    setDropActive(true);
  }

  function handleDrop(event: DragEvent<HTMLElement>): void {
    setDropActive(false);
    if (transfer?.onReceive === undefined || !transfer.canReceive) {
      return;
    }
    const dragged = readDraggedPaths(event.dataTransfer, transfer.acceptsFrom);
    if (dragged === null || dragged.paths.length === 0) {
      return;
    }
    event.preventDefault();
    transfer.onReceive(dragged.paths);
  }

  return (
    <section
      className={dropActive ? 'file-pane file-pane-drop' : 'file-pane'}
      aria-label={`${title} files`}
      onDragOver={handleDragOver}
      onDragLeave={() => setDropActive(false)}
      onDrop={handleDrop}
    >
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
        {onOpenFile === undefined ? null : (
          <button
            type="button"
            className="btn btn-sm btn-ghost"
            disabled={selectedEntries.length !== 1 || selectedEntries[0]?.kind !== 'File'}
            onClick={() => selectedEntries[0] !== undefined && onOpenFile(selectedEntries[0])}
          >
            <FileIcon size={13} />
            Edit
          </button>
        )}
        <button
          type="button"
          className="btn btn-sm btn-ghost btn-danger-link"
          disabled={selectedEntries.length === 0 || busy}
          onClick={() => openAction({ type: 'delete', entries: selectedEntries })}
        >
          Delete
        </button>
        {search !== undefined && (
          <button
            type="button"
            className="icon-btn file-search-toggle"
            aria-label={searchOpen ? 'Close file search' : 'Search files'}
            aria-pressed={searchOpen}
            title={searchOpen ? 'Close file search' : 'Search files'}
            disabled={currentPath === null}
            onClick={() => setSearchOpen((open) => !open)}
          >
            <SearchIcon size={14} />
          </button>
        )}
        {transfer !== undefined && (
          <button
            type="button"
            className={transfer.primary ? 'btn btn-sm btn-primary file-transfer-button' : 'btn btn-sm btn-filled file-transfer-button'}
            disabled={selectedEntries.length === 0 || !transfer.canSend}
            title={transfer.canSend ? undefined : transfer.unavailableReason}
            onClick={() => transfer.onSend(selectedEntries.map((item) => item.path))}
          >
            <transfer.Icon size={13} />
            {transfer.label}
          </button>
        )}
        <RegionToggle />
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
      {showSearch && (
        <FileSearchPanel title={title} search={search} rootPath={currentPath} onOpenLocation={openSearchLocation} />
      )}
      {!showSearch && loading && <EmptyState message="Loading..." />}
      {!showSearch && listing !== null && entries.length === 0 && <EmptyState message="This folder is empty." />}
      {!showSearch && listing !== null && entries.length > 0 && (
        <div className="file-table-scroll">
          <FileTable
            entries={entries}
            selected={selectedPaths}
            sortKey={sortKey}
            sortDirection={sortDirection}
            onSort={handleSort}
            onSelect={handleSelect}
            onOpen={handleOpen}
            onDragStart={transfer === undefined ? undefined : handleDragStart}
          />
        </div>
      )}
    </section>
  );
}
