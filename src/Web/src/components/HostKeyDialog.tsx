import { useState } from 'react';
import type { HostKeyDecision, PendingHostKey } from '../api/hostKeys';
import { ShieldAlertIcon, ShieldIcon } from './icons/Icons';

export interface HostKeyDialogProps {
  pending: PendingHostKey;
  error: string | null;
  busy: boolean;
  onDecide: (decision: HostKeyDecision, confirmReplace: boolean) => void;
}

function formatEndpoint(pending: PendingHostKey): string {
  return pending.port === 22 ? pending.host : `${pending.host}:${pending.port}`;
}

export function HostKeyDialog({ pending, error, busy, onDecide }: HostKeyDialogProps) {
  const [confirmReplace, setConfirmReplace] = useState(false);
  const isChanged = pending.status === 'changed';
  const endpoint = formatEndpoint(pending);
  const titleId = `host-key-title-${pending.id}`;

  return (
    <div className="dialog-backdrop">
      <div role="dialog" aria-modal="true" aria-labelledby={titleId} className="dialog-card">
        <div className="dialog-header">
          <span className={isChanged ? 'host-key-icon callout-danger' : 'host-key-icon callout-info'}>
            {isChanged ? <ShieldAlertIcon size={22} /> : <ShieldIcon size={22} />}
          </span>
          <h2 id={titleId} className="dialog-title">
            {isChanged ? `Host key CHANGED for ${endpoint}` : `Unknown host key for ${endpoint}`}
          </h2>
          {isChanged ? (
            <p className="notice notice-danger">
              WARNING: the server presented a different key than the one you trust. This can mean a man-in-the-middle
              attack. The connection is blocked.
            </p>
          ) : (
            <p className="text-secondary">
              The authenticity of this host cannot be established. Verify the fingerprint before you continue.
            </p>
          )}
        </div>
        <div className="dialog-body">
          <dl className={isChanged ? 'fingerprint fingerprint-danger' : 'fingerprint'}>
            <div className="fingerprint-meta">
              <dt>Presented key</dt>
              <dd>{pending.presentedKey.algorithm}</dd>
            </div>
            <dt className="visually-hidden">Fingerprint</dt>
            <dd>
              <code className="fingerprint-value">{pending.presentedKey.fingerprint}</code>
            </dd>
          </dl>
          {pending.knownKeys.length > 0 ? (
            <div className="host-key-known">
              <p className="field-label">Previously trusted keys:</p>
              <ul aria-label="Previously trusted keys" className="host-key-list">
                {pending.knownKeys.map((key) => (
                  <li key={key.fingerprint} className="fingerprint">
                    <span className="fingerprint-meta">{key.algorithm}</span>
                    <code className="fingerprint-value">{key.fingerprint}</code>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
          <p className="field-hint">
            Decide before {new Date(pending.expiresAt).toLocaleTimeString()}; without a decision the connection fails.
          </p>
          {error === null ? null : <p className="notice notice-danger">{error}</p>}
          {isChanged ? (
            <label className="check-row">
              <input
                type="checkbox"
                className="checkbox"
                checked={confirmReplace}
                onChange={(event) => setConfirmReplace(event.target.checked)}
              />
              I verified the new fingerprint and want to replace the saved key
            </label>
          ) : null}
        </div>
        {isChanged ? (
          <div className="dialog-footer">
            <button
              type="button"
              className="btn btn-destructive"
              disabled={busy || !confirmReplace}
              onClick={() => onDecide('replace', true)}
            >
              Replace saved key
            </button>
            <button type="button" className="btn btn-safe" disabled={busy} onClick={() => onDecide('reject', false)}>
              Reject
            </button>
          </div>
        ) : (
          <div className="dialog-footer">
            <button type="button" className="btn" disabled={busy} onClick={() => onDecide('reject', false)}>
              Reject
            </button>
            <button type="button" className="btn" disabled={busy} onClick={() => onDecide('acceptOnce', false)}>
              Accept once
            </button>
            <button
              type="button"
              className="btn btn-primary"
              disabled={busy}
              onClick={() => onDecide('acceptAndSave', false)}
            >
              Accept and save
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
