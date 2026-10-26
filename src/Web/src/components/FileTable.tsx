import {
  formatSize,
  isNavigable,
  type FileEntry,
  type SortDirection,
  type SortKey,
} from '../api/files';

export interface FileTableProps {
  entries: FileEntry[];
  selected: ReadonlySet<string>;
  sortKey: SortKey;
  sortDirection: SortDirection;
  onSort: (key: SortKey) => void;
  onSelect: (entry: FileEntry, additive: boolean) => void;
  onOpen: (entry: FileEntry) => void;
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

export function FileTable(props: FileTableProps) {
  const { entries, selected, sortKey, sortDirection, onSort, onSelect, onOpen } = props;
  const sortMarker = sortDirection === 'asc' ? ' ^' : ' v';
  return (
    <table className="file-table">
      <thead>
        <tr>
          {COLUMNS.map((column) => (
            <th key={column.key} aria-sort={ariaSort(sortKey === column.key, sortDirection)}>
              <button type="button" onClick={() => onSort(column.key)}>
                {column.label}
                {sortKey === column.key ? sortMarker : ''}
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
          >
            <td>{isNavigable(entry) ? `${entry.name}/` : entry.name}</td>
            <td>{formatSize(entry)}</td>
            <td>{entry.modifiedUtc ?? ''}</td>
            <td>{describeKind(entry)}</td>
            <td>{entry.permissions ?? ''}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}
