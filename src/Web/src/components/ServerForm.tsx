import { useState, type FormEvent, type KeyboardEvent } from 'react';
import {
  AUTH_METHODS,
  describeError,
  RECONNECT_MODES,
  type AuthMethod,
  type ConnectionInput,
  type ReconnectMode,
  type SavedConnection,
  revealPassword,
} from '../api/connections';
import { CloseIcon, EyeIcon, EyeOffIcon } from './icons/Icons';
import { Switch } from './Switch';

export interface ServerFormProps {
  title: string;
  initial: ConnectionInput;
  editingId: string | null;
  connections: readonly SavedConnection[];
  hasStoredPassword: boolean;
  onSubmit: (input: ConnectionInput) => Promise<void>;
  onCancel: () => void;
}

const AUTH_OPTIONS: Record<AuthMethod, { title: string; hint: string }> = {
  Agent: { title: 'SSH agent', hint: 'Uses keys loaded in your agent' },
  KeyFile: { title: 'Key file', hint: 'Path only, never copied' },
  Password: { title: 'Password', hint: 'Kept in the credential store' },
};

function reconnectModeLabel(mode: ReconnectMode, attempts: number): string {
  if (mode === 'Automatic') {
    return `Reconnect ${attempts} times, then stop`;
  }
  return mode === 'Manual' ? 'Ask me' : 'Don’t reconnect';
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
  const [passwordVisible, setPasswordVisible] = useState(false);

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

  const togglePasswordVisibility = () => {
    if (passwordVisible) {
      setPasswordVisible(false);
      return;
    }
    if (editingId === null || !hasStoredPassword || input.password !== '') {
      setPasswordVisible(true);
      return;
    }
    setError(null);
    revealPassword(editingId)
      .then((password) => {
        update('password', password);
        setPasswordVisible(true);
      })
      .catch((failure: unknown) => setError(describeError(failure)));
  };

  const jumpCandidates = connections.filter((connection) => connection.id !== editingId);
  const groupNames = [...new Set(connections.map((connection) => connection.groupName).filter((name): name is string => name !== null))];

  const cancelOnEscape = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape') {
      onCancel();
    }
  };

  return (
    <div className="dialog-backdrop">
      <div
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className="dialog-card dialog-card-wide server-dialog"
        onKeyDown={cancelOnEscape}
      >
        <form onSubmit={submit}>
          <div className="dialog-header">
            <h2 className="dialog-title">{title}</h2>
            <button type="button" className="icon-btn" aria-label="Close" onClick={onCancel}>
              <CloseIcon size={16} />
            </button>
          </div>
          <div className="dialog-body">
            <div className="server-form-grid">
              <label className="field span-2">
                <span className="field-label">Name</span>
                <input
                  className="input"
                  value={input.name}
                  onChange={(event) => update('name', event.target.value)}
                  autoFocus
                  required
                />
              </label>
              <label className="field span-2">
                <span className="field-label">Group</span>
                <input
                  className="input"
                  list="server-group-names"
                  value={input.groupName}
                  onChange={(event) => update('groupName', event.target.value)}
                />
                <datalist id="server-group-names">
                  {groupNames.map((name) => (
                    <option key={name} value={name} />
                  ))}
                </datalist>
              </label>
              <label className="field span-2">
                <span className="field-label">Host or SSH alias</span>
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
              <fieldset className="auth-options span-4">
                <legend className="field-label">Authentication</legend>
                {AUTH_METHODS.map((method) => (
                  <label key={method} className="auth-option">
                    <input
                      type="radio"
                      name="authMethod"
                      value={method}
                      checked={input.authMethod === method}
                      aria-labelledby={`auth-title-${method}`}
                      aria-describedby={`auth-hint-${method}`}
                      onChange={() => update('authMethod', method)}
                    />
                    <span id={`auth-title-${method}`} className="auth-option-title">
                      {AUTH_OPTIONS[method].title}
                    </span>
                    <span id={`auth-hint-${method}`} className="auth-option-hint">
                      {AUTH_OPTIONS[method].hint}
                    </span>
                  </label>
                ))}
              </fieldset>
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
                <div className="field span-4">
                  <label className="field-label" htmlFor="server-password">
                    Server password
                  </label>
                  <div className="input-with-action">
                    <input
                      id="server-password"
                      className="input"
                      type={passwordVisible ? 'text' : 'password'}
                      autoComplete="new-password"
                      placeholder={hasStoredPassword ? 'Leave empty to keep the stored password' : ''}
                      value={input.password}
                      onChange={(event) => update('password', event.target.value)}
                    />
                    <button
                      type="button"
                      className="icon-btn"
                      aria-label={passwordVisible ? 'Hide password' : 'Show password'}
                      aria-pressed={passwordVisible}
                      onClick={togglePasswordVisibility}
                    >
                      {passwordVisible ? <EyeOffIcon size={16} /> : <EyeIcon size={16} />}
                    </button>
                  </div>
                </div>
              ) : null}
              <label className="field span-2">
                <span className="field-label">ProxyJump · optional</span>
                <select
                  className="input input-mono"
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
              <div className="field span-2">
                <label className="field">
                  <span className="field-label">If the connection drops</span>
                  <select
                    className="input"
                    aria-describedby="reconnect-hint"
                    value={input.reconnectMode}
                    onChange={(event) => update('reconnectMode', event.target.value as ReconnectMode)}
                  >
                    {RECONNECT_MODES.map((mode) => (
                      <option key={mode} value={mode}>
                        {reconnectModeLabel(mode, input.reconnectMaxAttempts)}
                      </option>
                    ))}
                  </select>
                </label>
                <span id="reconnect-hint" className="field-hint">
                  After reconnecting, TermForge opens a fresh shell and never re-runs the command that was running.
                </span>
              </div>
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
              <div className="server-form-toggle span-2">
                <Switch label="Read-only server" checked={input.readOnly} onChange={(checked) => update('readOnly', checked)} />
                <span className="server-form-toggle-text">
                  <span className="server-form-toggle-title">Read-only</span>
                  <span className="field-hint">Block uploads, saves and Docker actions on this server</span>
                </span>
              </div>
            </div>
            {error === null ? null : (
              <p role="alert" className="form-error">
                {error}
              </p>
            )}
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
