import { useState } from 'react';
import { describeError, inputFrom, updateConnection, type SavedConnection } from '../../api/connections';
import { getConnections, replaceConnections, useConnections } from '../../state/connections';
import { Switch } from '../Switch';

function replaceSaved(saved: SavedConnection): void {
  const next = getConnections().map((connection) => (connection.id === saved.id ? saved : connection));
  replaceConnections(next);
}

export function ReadOnlyServersSection() {
  const connections = useConnections();
  const [savingId, setSavingId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const toggle = async (connection: SavedConnection, readOnly: boolean) => {
    setSavingId(connection.id);
    setError(null);
    try {
      const input = { ...inputFrom(connection), readOnly };
      const saved = await updateConnection(connection.id, input);
      replaceSaved(saved);
    } catch (failure) {
      setError(describeError(failure));
    } finally {
      setSavingId(null);
    }
  };

  return (
    <section className="settings-section" aria-labelledby="settings-read-only-title">
      <div className="settings-section-header">
        <div className="settings-row-text">
          <h2 id="settings-read-only-title" className="settings-section-title">
            Read-only servers
          </h2>
          <span className="settings-row-hint">
            Uploads, saves and Docker actions are disabled. The terminal shows a read-only banner.
          </span>
        </div>
      </div>
      <div className="settings-card">
        {connections.length === 0 ? (
          <p className="settings-row settings-row-hint">No saved servers yet.</p>
        ) : (
          connections.map((connection) => (
            <div key={connection.id} className="settings-row read-only-server-row">
              <span className="read-only-server-name">{connection.name}</span>
              {connection.groupName === null ? null : (
                <span className="read-only-server-group">{connection.groupName}</span>
              )}
              <Switch
                label={`${connection.name} read-only`}
                checked={connection.readOnly}
                disabled={savingId !== null}
                onChange={(checked) => void toggle(connection, checked)}
              />
            </div>
          ))
        )}
        {error === null ? null : (
          <p role="alert" className="settings-row form-error">
            {error}
          </p>
        )}
      </div>
    </section>
  );
}
