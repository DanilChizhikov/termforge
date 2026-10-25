import { useState, type FormEvent } from 'react';
import {
  AUTH_METHODS,
  describeError,
  type AuthMethod,
  type ConnectionInput,
  type SavedConnection,
} from '../api/connections';

export interface ServerFormProps {
  title: string;
  initial: ConnectionInput;
  editingId: string | null;
  connections: SavedConnection[];
  hasStoredPassword: boolean;
  onSubmit: (input: ConnectionInput) => Promise<void>;
  onCancel: () => void;
}

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
    <div role="dialog" aria-label={title} className="dialog">
      <form onSubmit={submit}>
        <h3>{title}</h3>
        <label>
          Name
          <input value={input.name} onChange={(event) => update('name', event.target.value)} required />
        </label>
        <label>
          Group
          <input value={input.groupName} onChange={(event) => update('groupName', event.target.value)} />
        </label>
        <label>
          Host
          <input value={input.host} onChange={(event) => update('host', event.target.value)} required />
        </label>
        <label>
          Port
          <input
            type="number"
            min={1}
            max={65535}
            value={input.port}
            onChange={(event) => update('port', Number(event.target.value))}
          />
        </label>
        <label>
          User
          <input value={input.userName} onChange={(event) => update('userName', event.target.value)} required />
        </label>
        <label>
          Authentication
          <select
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
        {input.authMethod === 'KeyFile' ? (
          <label>
            Private key file
            <input
              value={input.privateKeyPath}
              onChange={(event) => update('privateKeyPath', event.target.value)}
              required
            />
          </label>
        ) : null}
        {input.authMethod === 'Password' ? (
          <label>
            Password
            <input
              type="password"
              autoComplete="new-password"
              placeholder={hasStoredPassword ? 'Leave empty to keep the stored password' : ''}
              value={input.password}
              onChange={(event) => update('password', event.target.value)}
            />
          </label>
        ) : null}
        <label>
          Jump host
          <select value={input.jumpHostId} onChange={(event) => update('jumpHostId', event.target.value)}>
            <option value="">None</option>
            {jumpCandidates.map((connection) => (
              <option key={connection.id} value={connection.id}>
                {connection.name}
              </option>
            ))}
          </select>
        </label>
        {error === null ? null : <p className="form-error">{error}</p>}
        <div className="dialog-actions">
          <button type="submit" disabled={busy}>
            Save
          </button>
          <button type="button" onClick={onCancel}>
            Cancel
          </button>
        </div>
      </form>
    </div>
  );
}
