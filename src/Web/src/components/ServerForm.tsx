import { useState, type FormEvent } from 'react';
import {
  AUTH_METHODS,
  describeError,
  RECONNECT_MODES,
  type AuthMethod,
  type ConnectionInput,
  type ReconnectMode,
  type SavedConnection,
} from '../api/connections';

export interface ServerFormProps {
  title: string;
  initial: ConnectionInput;
  editingId: string | null;
  connections: readonly SavedConnection[];
  hasStoredPassword: boolean;
  onSubmit: (input: ConnectionInput) => Promise<void>;
  onCancel: () => void;
}

const RECONNECT_MODE_LABELS: Record<ReconnectMode, string> = {
  Manual: 'Ask me (show a Reconnect button)',
  Automatic: 'Reconnect automatically',
  Off: 'Don’t reconnect',
};

export function ServerForm({
  title,
  initial,
  editingId,
  connections,
  hasStoredPassword,
  onSubmit,
  onCancel,
}: ServerFormProps) {
  const [input, setInput] = useState<ConnectionInput>(initial);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const update = <K extends keyof ConnectionInput>(key: K, value: ConnectionInput[K]) => {
    setInput((current) => ({ ...current, [key]: value }));
  };

  const submit = (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    onSubmit(input).catch((failure: unknown) => {
      setError(describeError(failure));
      setBusy(false);
    });
  };

  const jumpCandidates = connections.filter((connection) => connection.id !== editingId);

  return (
    <div className="dialog-backdrop">
      <div role="dialog" aria-label={title} className="dialog-card dialog-card-wide">
        <form onSubmit={submit}>
          <div className="dialog-header">
            <h2 className="dialog-title">{title}</h2>
          </div>
          <div className="dialog-body">
            <div className="server-form-grid">
              <label className="field span-2">
                <span className="field-label">Name</span>
                <input
                  className="input"
                  value={input.name}
                  onChange={(event) => update('name', event.target.value)}
                  required
                />
              </label>
              <label className="field span-2">
                <span className="field-label">Group</span>
                <input
                  className="input"
                  value={input.groupName}
                  onChange={(event) => update('groupName', event.target.value)}
                />
              </label>
              <label className="field span-2">
                <span className="field-label">Host</span>
                <input
                  className="input input-mono"
                  value={input.host}
                  onChange={(event) => update('host', event.target.value)}
                  required
                />
              </label>
              <label className="field">
                <span className="field-label">Port</span>
                <input
                  className="input input-mono"
                  type="number"
                  min={1}
                  max={65535}
                  value={input.port}
                  onChange={(event) => update('port', Number(event.target.value))}
                />
              </label>
              <label className="field">
                <span className="field-label">User</span>
                <input
                  className="input input-mono"
                  value={input.userName}
                  onChange={(event) => update('userName', event.target.value)}
                  required
                />
              </label>
              <label className="field span-2">
                <span className="field-label">Authentication</span>
                <select
                  className="input"
                  value={input.authMethod}
                  onChange={(event) => update('authMethod', event.target.value as AuthMethod)}
                >
                  {AUTH_METHODS.map((method) => (
                    <option key={method} value={method}>
                      {method}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field span-2">
                <span className="field-label">Jump host</span>
                <select
                  className="input"
                  value={input.jumpHostId}
                  onChange={(event) => update('jumpHostId', event.target.value)}
                >
                  <option value="">None</option>
                  {jumpCandidates.map((connection) => (
                    <option key={connection.id} value={connection.id}>
                      {connection.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="field span-4">
                <span className="field-label">If the connection drops</span>
                <select
                  className="input"
                  value={input.reconnectMode}
                  onChange={(event) => update('reconnectMode', event.target.value as ReconnectMode)}
                >
                  {RECONNECT_MODES.map((mode) => (
                    <option key={mode} value={mode}>
                      {RECONNECT_MODE_LABELS[mode]}
                    </option>
                  ))}
                </select>
                <span className="field-hint">
                  After reconnecting, TermForge opens a fresh shell and never re-runs the command that was running.
                </span>
              </label>
              {input.reconnectMode === 'Automatic' ? (
                <>
                  <label className="field span-2">
                    <span className="field-label">Attempts before giving up</span>
                    <input
                      className="input input-mono"
                      type="number"
                      min={1}
                      max={20}
                      value={input.reconnectMaxAttempts}
                      onChange={(event) => update('reconnectMaxAttempts', Number(event.target.value))}
                    />
                  </label>
                  <label className="field">
                    <span className="field-label">First wait (s)</span>
                    <input
                      className="input input-mono"
                      type="number"
                      min={1}
                      max={3600}
                      value={input.reconnectInitialDelaySeconds}
                      onChange={(event) => update('reconnectInitialDelaySeconds', Number(event.target.value))}
                    />
                  </label>
                  <label className="field">
                    <span className="field-label">Longest wait (s)</span>
                    <input
                      className="input input-mono"
                      type="number"
                      min={1}
                      max={3600}
                      value={input.reconnectMaxDelaySeconds}
                      onChange={(event) => update('reconnectMaxDelaySeconds', Number(event.target.value))}
                    />
                  </label>
                </>
              ) : null}
              {input.authMethod === 'KeyFile' ? (
                <label className="field span-4">
                  <span className="field-label">Private key file</span>
                  <input
                    className="input input-mono"
                    value={input.privateKeyPath}
                    onChange={(event) => update('privateKeyPath', event.target.value)}
                    required
                  />
                </label>
              ) : null}
              {input.authMethod === 'Password' ? (
                <label className="field span-4">
                  <span className="field-label">Password</span>
                  <input
                    className="input"
                    type="password"
                    autoComplete="new-password"
                    placeholder={hasStoredPassword ? 'Leave empty to keep the stored password' : ''}
                    value={input.password}
                    onChange={(event) => update('password', event.target.value)}
                  />
                </label>
              ) : null}
            </div>
            {error === null ? null : <p className="form-error">{error}</p>}
          </div>
          <div className="dialog-footer">
            <button type="button" className="btn" onClick={onCancel}>
              Cancel
            </button>
            <button type="submit" className="btn btn-primary" disabled={busy}>
              Save
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
