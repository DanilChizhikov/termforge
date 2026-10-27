import type { UpdateStatus } from '../../api/updates';
import { checkUpdatesNow, useUpdates } from '../../state/updates';

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

  if (status === null) {
    return null;
  }

  return (
    <section id="settings-updates" className="settings-info-card" aria-label="Updates" data-testid="updates-card">
      <span className="settings-info-label">Updates</span>
      <span className="settings-info-value">
        {describeStatus(status)} <span className="settings-info-aside">· you have v{status.currentVersion}</span>
      </span>
      <span className="settings-info-detail">Checked against GitHub Releases. TermForge never updates itself.</span>
      <div className="settings-info-actions">
        {status.state === 'updateavailable' && status.releaseUrl !== null ? (
          <a href={status.releaseUrl} target="_blank" rel="noopener noreferrer">
            Open the release page
          </a>
        ) : null}
        <button type="button" className="btn btn-sm" onClick={() => void checkUpdatesNow()} disabled={checking}>
          {checking ? 'Checking…' : 'Check for updates'}
        </button>
      </div>
    </section>
  );
}
