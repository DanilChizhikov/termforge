import { showAppView } from '../state/appView';
import { useUpdates } from '../state/updates';

export function UpdateNotice() {
  const { status } = useUpdates();

  if (status === null || status.state !== 'updateavailable' || status.latestVersion === null) {
    return null;
  }

  return (
    <button
      type="button"
      className="update-pill"
      data-testid="update-notice"
      onClick={() => showAppView('settings')}
    >
      Update v{status.latestVersion} available
    </button>
  );
}
