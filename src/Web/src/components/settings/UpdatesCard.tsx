import type { UpdateStatus } from '../../api/updates';
import { checkUpdatesNow, useUpdates } from '../../state/updates';
import { useEffect, useState } from 'react';
import { getInstallationStatus, type InstallationStatus } from '../../api/installation';
import { UpdateInstallDialog } from './UpdateInstallDialog';

function describeStatus(status: UpdateStatus): string {
  if (status.state === 'updateavailable' && status.latestVersion !== null) {
    return `v${status.latestVersion} is available`;
  }
  if (status.state === 'uptodate') {
    return 'Up to date';
  }
  if (status.state === 'checkfailed') {
    return 'Update check unavailable';
  }
  return 'Not checked yet';
}

export function UpdatesCard() {
  const { status, checking } = useUpdates();
  const [installation, setInstallation] = useState<InstallationStatus | null>(null);
  const [mode, setMode] = useState<'update' | 'rollback' | null>(null);

  useEffect(() => {
    let active = true;
    void getInstallationStatus().then((value) => { if (active) setInstallation(value); }).catch(() => undefined);
    return () => { active = false; };
  }, [mode]);

  if (status === null) {
    return null;
  }

  return (
    <section id="settings-updates" className="settings-info-card" aria-label="Updates" data-testid="updates-card">
      <span className="settings-info-label">Updates</span>
      <span className="settings-info-value">
        {describeStatus(status)} <span className="settings-info-aside">· you have v{status.currentVersion}</span>
      </span>
      <span className="settings-info-detail">Checked against GitHub Releases. Installation starts only when you choose it.</span>
      {installation?.unavailableReason && <span className="settings-info-detail">{installation.unavailableReason}</span>}
      {installation?.error && <p role="alert" className="notice notice-danger">{installation.error}</p>}
      <div className="settings-info-actions">
        {status.state === 'updateavailable' && status.releaseUrl !== null ? (
          <a href={status.releaseUrl} target="_blank" rel="noopener noreferrer">
            Open the release page
          </a>
        ) : null}
        <button type="button" className="btn btn-sm" onClick={() => void checkUpdatesNow()} disabled={checking}>
          {checking ? 'Checking…' : 'Check for updates'}
        </button>
        {status.state === 'updateavailable' && installation?.supported && <button type="button" className="btn btn-sm btn-primary" onClick={() => setMode('update')}>Install update</button>}
        {installation?.previousVersion && <button type="button" className="btn btn-sm" onClick={() => setMode('rollback')}>Roll back to v{installation.previousVersion}</button>}
      </div>
      {mode !== null && <UpdateInstallDialog rollback={mode === 'rollback'} onClose={() => setMode(null)} />}
    </section>
  );
}
