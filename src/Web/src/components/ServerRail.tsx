import type { SavedConnection } from '../api/connections';
import { RegionToggle } from './CollapsibleRegion';
import { serverInitials } from './serverInitials';
import { describeServerStatus, type ServerStatus } from './serverStatus';
import { statusDotClass } from './statusTone';

export interface ServerRailProps {
  groups: ReadonlyMap<string, readonly SavedConnection[]>;
  statuses: Readonly<Record<string, ServerStatus>>;
  selectedId: string | null;
  onSelect: (connectionId: string) => void;
}

function describeRailButton(connection: SavedConnection, status: ServerStatus | null): string {
  const { label } = describeServerStatus(connection, status);
  const parts = [connection.name, label.toLowerCase()];
  if (connection.readOnly) {
    parts.push('read-only');
  }
  return parts.join(', ');
}

export function ServerRail({ groups, statuses, selectedId, onSelect }: ServerRailProps) {
  const entries = [...groups.entries()];
  return (
    <nav className="server-rail" aria-label="Servers, collapsed">
      <RegionToggle />
      {entries.map(([group, connections], groupIndex) => (
        <div key={group} className="server-rail-group">
          {groupIndex === 0 ? null : <span className="server-rail-separator" aria-hidden="true" />}
          {connections.map((connection) => {
            const status = statuses[connection.id] ?? null;
            const { tone } = describeServerStatus(connection, status);
            const selected = connection.id === selectedId;
            return (
              <button
                key={connection.id}
                type="button"
                className={selected ? 'server-rail-button server-rail-button-selected' : 'server-rail-button'}
                aria-label={describeRailButton(connection, status)}
                aria-pressed={selected}
                title={connection.name}
                onClick={() => onSelect(connection.id)}
              >
                <code>{serverInitials(connection.name)}</code>
                <span className={`${statusDotClass(tone)} server-rail-status`} aria-hidden="true" />
              </button>
            );
          })}
        </div>
      ))}
    </nav>
  );
}
