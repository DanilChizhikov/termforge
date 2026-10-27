import type { DragEvent } from 'react';
import {
  formatSize,
  isNavigable,
  type FileEntry,
  type SortDirection,
  type SortKey,
} from '../api/files';
import { FileIcon, FolderIcon, LinkIcon, SortAscendingIcon, SortDescendingIcon } from './icons/Icons';
import { formatCompactTimestamp } from './timeFormat';

export interface FileTableProps {
  entries: FileEntry[];
  selected: ReadonlySet<string>;
  sortKey: SortKey;
  sortDirection: SortDirection;
  onSort: (key: SortKey) => void;
  onSelect: (entry: FileEntry, additive: boolean) => void;
  onOpen: (entry: FileEntry) => void;
  onDragStart?: ((entry: FileEntry, event: DragEvent<HTMLElement>) => void) | undefined;
  parentPath?: string | null;
  onNavigate?: ((path: string) => void) | undefined;
}

const COLUMNS: ReadonlyArray<{ key: SortKey; label: string }> = [
  { key: 'name', label: 'Name' },
  { key: 'size', label: 'Size' },
  { key: 'modified', label: 'Modified' },
];

function describeKind(entry: FileEntry): string {
  if (entry.kind !== 'SymbolicLink') {
    return entry.kind;
  }
  if (entry.isBrokenLink) {
    return 'Broken link';
  }
  return entry.linkTargetKind === null ? 'Link' : `Link to ${entry.linkTargetKind}`;
}

function ariaSort(active: boolean, direction: SortDirection): 'ascending' | 'descending' | 'none' {
  if (!active) {
    return 'none';
  }
  return direction === 'asc' ? 'ascending' : 'descending';
}

function EntryIcon({ entry }: { entry: FileEntry }) {
  if (isNavigable(entry)) {
    return (
      <span className="file-icon file-icon-folder">
        <FolderIcon />
      </span>
    );
  }
  return <span className="file-icon">{entry.kind === 'SymbolicLink' ? <LinkIcon /> : <FileIcon />}</span>;
}

function SortMarker({ direction }: { direction: SortDirection }) {
  return direction === 'asc' ? <SortAscendingIcon /> : <SortDescendingIcon />;
}

function formatModified(modifiedUtc: string | null): string {
  return modifiedUtc === null ? '' : formatCompactTimestamp(modifiedUtc);
}

function describeEntry(entry: FileEntry): string {
  const permissions = entry.permissions === null ? '' : ` · ${entry.permissions}`;
  const modified = entry.modifiedUtc === null ? '' : ` · ${new Date(entry.modifiedUtc).toLocaleString()}`;
  return `${describeKind(entry)}${permissions}${modified}`;
}

export function FileTable(props: FileTableProps) {
  const { entries, selected, sortKey, sortDirection, onSort, onSelect, onOpen, onDragStart, parentPath = null, onNavigate } = props;
  return (
    <table className="data-table">
      <thead>
        <tr>
          {COLUMNS.map((column) => (
            <th
              key={column.key}
              className={column.key === 'name' ? undefined : `cell-end file-column-${column.key}`}
              aria-sort={ariaSort(sortKey === column.key, sortDirection)}
            >
              <button type="button" className="th-button" onClick={() => onSort(column.key)}>
                {column.label}
                {sortKey === column.key ? <SortMarker direction={sortDirection} /> : null}
              </button>
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {parentPath === null || onNavigate === undefined ? null : (
          <tr className="file-row file-row-parent">
            <td colSpan={COLUMNS.length}>
              <button type="button" className="file-parent" aria-label="Parent folder" onClick={() => onNavigate(parentPath)}>
                ..
              </button>
            </td>
          </tr>
        )}
        {entries.map((entry) => (
          <tr
            key={entry.path}
            aria-selected={selected.has(entry.path)}
            className={selected.has(entry.path) ? 'file-row file-row-selected' : 'file-row'}
            onClick={(event) => onSelect(entry, event.ctrlKey || event.metaKey)}
            onDoubleClick={() => onOpen(entry)}
            draggable={onDragStart !== undefined}
            onDragStart={onDragStart === undefined ? undefined : (event) => onDragStart(entry, event)}
            title={describeEntry(entry)}
          >
            <td>
              <span className="file-name">
                <EntryIcon entry={entry} />
                {entry.name}
              </span>
            </td>
            <td className="cell-end cell-muted">{entry.kind === 'Directory' ? <span className="text-muted">—</span> : formatSize(entry)}</td>
            <td className="cell-end cell-muted">{formatModified(entry.modifiedUtc)}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
