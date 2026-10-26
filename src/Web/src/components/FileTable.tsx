import type { DragEvent } from 'react';
import {
  formatSize,
  isNavigable,
  type FileEntry,
  type SortDirection,
  type SortKey,
} from '../api/files';
import { FileIcon, FolderIcon, LinkIcon, SortAscendingIcon, SortDescendingIcon } from './icons/Icons';

export interface FileTableProps {
  entries: FileEntry[];
  selected: ReadonlySet<string>;
  sortKey: SortKey;
  sortDirection: SortDirection;
  onSort: (key: SortKey) => void;
  onSelect: (entry: FileEntry, additive: boolean) => void;
  onOpen: (entry: FileEntry) => void;
  onDragStart?: ((entry: FileEntry, event: DragEvent<HTMLElement>) => void) | undefined;
}

const COLUMNS: ReadonlyArray<{ key: SortKey; label: string }> = [
  { key: 'name', label: 'Name' },
  { key: 'size', label: 'Size' },
  { key: 'modified', label: 'Modified' },
  { key: 'kind', label: 'Type' },
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
  return modifiedUtc === null ? '' : new Date(modifiedUtc).toLocaleString();
}

export function FileTable(props: FileTableProps) {
  const { entries, selected, sortKey, sortDirection, onSort, onSelect, onOpen, onDragStart } = props;
  return (
    <table className="data-table">
      <thead>
        <tr>
          {COLUMNS.map((column) => (
            <th
              key={column.key}
              className={column.key === 'size' ? 'cell-end' : undefined}
              aria-sort={ariaSort(sortKey === column.key, sortDirection)}
            >
              <button type="button" className="th-button" onClick={() => onSort(column.key)}>
                {column.label}
                {sortKey === column.key ? <SortMarker direction={sortDirection} /> : null}
              </button>
            </th>
          ))}
          <th>Permissions</th>
        </tr>
      </thead>
      <tbody>
        {entries.map((entry) => (
          <tr
            key={entry.path}
            aria-selected={selected.has(entry.path)}
            className={selected.has(entry.path) ? 'file-row file-row-selected' : 'file-row'}
            onClick={(event) => onSelect(entry, event.ctrlKey || event.metaKey)}
            onDoubleClick={() => onOpen(entry)}
            draggable={onDragStart !== undefined}
            onDragStart={onDragStart === undefined ? undefined : (event) => onDragStart(entry, event)}
          >
            <td>
              <span className="file-name">
                <EntryIcon entry={entry} />
                {isNavigable(entry) ? `${entry.name}/` : entry.name}
              </span>
            </td>
            <td className="cell-end cell-muted">{formatSize(entry)}</td>
            <td className="cell-muted">{formatModified(entry.modifiedUtc)}</td>
            <td className="cell-muted">{describeKind(entry)}</td>
            <td className="cell-mono cell-muted">{entry.permissions ?? ''}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
