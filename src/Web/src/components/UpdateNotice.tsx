import { useCallback, useEffect, useState } from 'react';
import { checkForUpdates, getUpdateStatus, type UpdateStatus } from '../api/updates';

export function UpdateNotice() {
  const [status, setStatus] = useState<UpdateStatus | null>(null);
  const [checking, setChecking] = useState(false);

  useEffect(() => {
    let active = true;
    getUpdateStatus()
      .then((result) => {
        if (active) {
          setStatus(result);
        }
      })
      .catch(() => undefined);
    return () => {
      active = false;
    };
  }, []);

  const checkNow = useCallback(() => {
    setChecking(true);
    checkForUpdates()
      .then(setStatus)
      .catch(() => undefined)
      .finally(() => setChecking(false));
  }, []);

  if (status === null) {
    return null;
  }

  return (
    <div className="update-notice" data-testid="update-notice">
      <span>TermForge {status.currentVersion}</span>
      {status.state === 'updateavailable' && status.releaseUrl !== null && (
        <span>
          {' '}
          | Update available: {status.latestVersion}{' '}
          <a href={status.releaseUrl} target="_blank" rel="noopener noreferrer">
            Download
          </a>
        </span>
      )}
      {status.state === 'uptodate' && <span> | Up to date</span>}
      {status.state === 'checkfailed' && <span> | Update check unavailable</span>}{' '}
      <button type="button" onClick={checkNow} disabled={checking}>
        {checking ? 'Checking...' : 'Check for updates'}
      </button>
    </div>
  );
}
