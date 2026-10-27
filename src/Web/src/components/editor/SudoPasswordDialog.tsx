import { useState, type FormEvent } from 'react';
import type { SudoCredentials, SudoPasswordProblem } from '../../api/sudoEditor';
import { ShieldIcon } from '../icons/Icons';
import { Switch } from '../Switch';

export interface SudoPasswordDialogProps {
  actionLabel: string;
  problem: SudoPasswordProblem | null;
  busy: boolean;
  onSubmit: (credentials: SudoCredentials) => void;
  onCancel: () => void;
}

export function SudoPasswordDialog({ actionLabel, problem, busy, onSubmit, onCancel }: SudoPasswordDialogProps) {
  const [password, setPassword] = useState('');
  const [remember, setRemember] = useState(false);

  function submit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    if (password.length === 0 || busy) {
      return;
    }
    onSubmit({ password, rememberPassword: remember });
    setPassword('');
  }

  return (
    <div className="dialog-backdrop">
      <form
        role="dialog"
        aria-modal="true"
        aria-labelledby="sudo-password-title"
        aria-describedby="sudo-password-description"
        className="dialog-card"
        onSubmit={submit}
      >
        <div className="dialog-header">
          <h2 id="sudo-password-title" className="dialog-title sudo-dialog-title">
            <ShieldIcon size={16} />
            Sudo password
          </h2>
          <p id="sudo-password-description" className="text-secondary">
            {actionLabel} needs sudo on the server. The password is sent for this action only and is not kept.
          </p>
        </div>
        <div className="dialog-body">
          {problem === 'SudoPasswordIncorrect' ? (
            <p className="notice notice-danger">The sudo password is incorrect. Nothing was changed.</p>
          ) : null}
          <label className="field">
            <span className="field-label">Password</span>
            <input
              className="input"
              type="password"
              autoComplete="off"
              autoFocus
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          </label>
          <div className="sudo-remember">
            <span className="sudo-remember-text">
              <span>Remember in credential store</span>
              <span className="field-hint">Later sudo actions on this server will not ask again.</span>
            </span>
            <Switch label="Remember in credential store" checked={remember} disabled={busy} onChange={setRemember} />
          </div>
        </div>
        <div className="dialog-footer">
          <button type="button" className="btn btn-safe" onClick={onCancel}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={busy || password.length === 0}>
            {busy ? 'Working…' : 'Continue with sudo'}
          </button>
        </div>
      </form>
    </div>
  );
}
