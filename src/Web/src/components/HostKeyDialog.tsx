import { useState, type KeyboardEvent } from 'react';
import type { HostKeyDecision, HostKeyInfo, PendingHostKey } from '../api/hostKeys';
import { ShieldAlertIcon, ShieldIcon } from './icons/Icons';

export interface HostKeyDialogProps {
  pending: PendingHostKey;
  error: string | null;
  busy: boolean;
  onDecide: (decision: HostKeyDecision, confirmReplace: boolean) => void;
}

const HOST_KEY_FILES: Record<string, string> = {
  'ssh-ed25519': 'ssh_host_ed25519_key.pub',
  'ssh-rsa': 'ssh_host_rsa_key.pub',
  'rsa-sha2-256': 'ssh_host_rsa_key.pub',
  'rsa-sha2-512': 'ssh_host_rsa_key.pub',
};

function formatEndpoint(pending: PendingHostKey): string {
  return `${pending.host}:${pending.port}`;
}

function describeAlgorithm(algorithm: string): string {
  if (algorithm.startsWith('ecdsa-')) {
    return 'ECDSA';
  }
  if (algorithm.startsWith('rsa-') || algorithm === 'ssh-rsa') {
    return 'RSA';
  }
  return algorithm.replace(/^ssh-/, '').toUpperCase();
}

function hostKeyFileOf(algorithm: string): string {
  if (algorithm.startsWith('ecdsa-')) {
    return 'ssh_host_ecdsa_key.pub';
  }
  return HOST_KEY_FILES[algorithm] ?? 'ssh_host_ed25519_key.pub';
}

function KeyFingerprint({ label, info, danger }: { label: string; info: HostKeyInfo; danger: boolean }) {
  return (
    <div className={danger ? 'fingerprint fingerprint-danger' : 'fingerprint'}>
      <div className="fingerprint-meta">
        <span>{label}</span>
        <span>{describeAlgorithm(info.algorithm)}</span>
      </div>
      <code className="fingerprint-value">{info.fingerprint}</code>
    </div>
  );
}

function UnknownKeyDialog({ pending, error, busy, onDecide }: HostKeyDialogProps) {
  const endpoint = formatEndpoint(pending);
  const titleId = `host-key-title-${pending.id}`;
  const descriptionId = `host-key-description-${pending.id}`;

  return (
    <div className="dialog-backdrop">
      <div role="dialog" aria-modal="true" aria-labelledby={titleId} aria-describedby={descriptionId} className="dialog-card">
        <div className="dialog-header">
          <h2 id={titleId} className="dialog-title">
            Unknown host key for {endpoint}
          </h2>
        </div>
        <div className="dialog-body">
          <div className="callout">
            <span className="callout-title callout-info">
              <ShieldIcon size={18} />
              First connection to this server
            </span>
            <p id={descriptionId} className="text-secondary">
              <code>{endpoint}</code> presented this key. Compare it with the fingerprint shown on the server before you
              trust it.
            </p>
            <KeyFingerprint label="Presented key · SHA256" info={pending.presentedKey} danger={false} />
            <p className="field-hint">
              On the server: <code>ssh-keygen -lf /etc/ssh/{hostKeyFileOf(pending.presentedKey.algorithm)}</code>
            </p>
          </div>
          {error === null ? null : <p className="notice notice-danger">{error}</p>}
        </div>
        <div className="dialog-footer">
          <button type="button" className="btn" disabled={busy} onClick={() => onDecide('reject', false)}>
            Reject
          </button>
          <button type="button" className="btn" disabled={busy} onClick={() => onDecide('acceptOnce', false)}>
            Accept once
          </button>
          <button type="button" className="btn btn-primary" disabled={busy} onClick={() => onDecide('acceptAndSave', false)}>
            Accept and save
          </button>
        </div>
      </div>
    </div>
  );
}

function ChangedKeyDialog({ pending, error, busy, onDecide }: HostKeyDialogProps) {
  const [typedHost, setTypedHost] = useState('');
  const endpoint = formatEndpoint(pending);
  const titleId = `host-key-title-${pending.id}`;
  const descriptionId = `host-key-description-${pending.id}`;
  const confirmed = typedHost.trim() === pending.host;

  const keepBlockedOnEscape = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape' && !busy) {
      onDecide('reject', false);
    }
  };

  return (
    <div className="dialog-backdrop">
      <div
        role="alertdialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={descriptionId}
        className="dialog-card dialog-card-danger"
        onKeyDown={keepBlockedOnEscape}
      >
        <div className="dialog-header host-key-header">
          <span className="host-key-icon-badge">
            <ShieldAlertIcon size={22} />
          </span>
          <h2 id={titleId} className="dialog-title">
            The host key for {pending.host} has changed
          </h2>
          <p id={descriptionId} className="text-secondary">
            Connection blocked. <code>{endpoint}</code> presented a different key than the one you trust. This happens
            when the server is reinstalled — or when someone is intercepting the connection.
          </p>
        </div>
        <div className="dialog-body">
          {pending.knownKeys.length > 0 ? (
            <ul aria-label="Previously trusted keys" className="host-key-list">
              {pending.knownKeys.map((key) => (
                <li key={key.fingerprint}>
                  <KeyFingerprint label="Trusted" info={key} danger={false} />
                </li>
              ))}
            </ul>
          ) : null}
          <KeyFingerprint label="Presented now" info={pending.presentedKey} danger />
          {error === null ? null : <p className="notice notice-danger">{error}</p>}
          <label className="field">
            <span className="field-label">
              Type <code>{pending.host}</code> to enable replacing
            </span>
            <input
              className="input input-mono"
              value={typedHost}
              autoComplete="off"
              spellCheck={false}
              onChange={(event) => setTypedHost(event.target.value)}
            />
          </label>
        </div>
        <div className="dialog-footer dialog-footer-plain">
          <button
            type="button"
            className="btn btn-destructive-outline"
            disabled={busy || !confirmed}
            onClick={() => onDecide('replace', true)}
          >
            Replace trusted key
          </button>
          <button type="button" className="btn btn-safe" autoFocus disabled={busy} onClick={() => onDecide('reject', false)}>
            Keep blocked
          </button>
        </div>
      </div>
    </div>
  );
}

export function HostKeyDialog(props: HostKeyDialogProps) {
  return props.pending.status === 'changed' ? <ChangedKeyDialog {...props} /> : <UnknownKeyDialog {...props} />;
}
