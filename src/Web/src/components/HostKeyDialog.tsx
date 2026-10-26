import { useState } from 'react';
import type { HostKeyDecision, PendingHostKey } from '../api/hostKeys';

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
    <div className="host-key-backdrop">
      <div role="dialog" aria-modal="true" aria-labelledby={titleId} className="host-key-dialog">
        <h2 id={titleId}>{isChanged ? `Host key CHANGED for ${endpoint}` : `Unknown host key for ${endpoint}`}</h2>
        {isChanged ? (
          <p className="host-key-warning">
            WARNING: the server presented a different key than the one you trust. This can mean a man-in-the-middle
            attack. The connection is blocked.
          </p>
        ) : (
          <p>The authenticity of this host cannot be established. Verify the fingerprint before you continue.</p>
        )}
        <dl>
          <dt>Algorithm</dt>
          <dd>{pending.presentedKey.algorithm}</dd>
          <dt>Fingerprint</dt>
          <dd>
            <code>{pending.presentedKey.fingerprint}</code>
          </dd>
        </dl>
        {pending.knownKeys.length > 0 ? (
          <div>
            <p>Previously trusted keys:</p>
            <ul aria-label="Previously trusted keys">
              {pending.knownKeys.map((key) => (
                <li key={key.fingerprint}>
                  {key.algorithm} <code>{key.fingerprint}</code>
                </li>
              ))}
            </ul>
          </div>
        ) : null}
        <p>Decide before {new Date(pending.expiresAt).toLocaleTimeString()}; without a decision the connection fails.</p>
        {error === null ? null : <p className="host-key-error">{error}</p>}
        {isChanged ? (
          <div>
            <label>
              <input
                type="checkbox"
                checked={confirmReplace}
                onChange={(event) => setConfirmReplace(event.target.checked)}
              />
              I verified the new fingerprint and want to replace the saved key
            </label>
            <div className="host-key-actions">
              <button type="button" disabled={busy} onClick={() => onDecide('reject', false)}>
                Reject
              </button>
              <button type="button" disabled={busy || !confirmReplace} onClick={() => onDecide('replace', true)}>
                Replace saved key
              </button>
            </div>
          </div>
        ) : (
          <div className="host-key-actions">
            <button type="button" disabled={busy} onClick={() => onDecide('reject', false)}>
              Reject
            </button>
            <button type="button" disabled={busy} onClick={() => onDecide('acceptOnce', false)}>
              Accept once
            </button>
            <button type="button" disabled={busy} onClick={() => onDecide('acceptAndSave', false)}>
              Accept and save
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
