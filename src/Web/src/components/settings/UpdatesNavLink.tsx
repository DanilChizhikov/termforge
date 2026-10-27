import { useUpdates } from '../../state/updates';

export function UpdatesNavLink() {
  const { status } = useUpdates();
  const available = status?.state === 'updateavailable';

  return (
    <a href="#settings-updates" className="settings-nav-link">
      Updates
      {available ? (
        <>
          <span className="status-dot status-dot-info" aria-hidden="true" />
          <span className="visually-hidden">(update available)</span>
        </>
      ) : null}
    </a>
  );
}
