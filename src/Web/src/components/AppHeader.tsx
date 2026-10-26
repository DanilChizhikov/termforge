import { showAppView, useAppView } from '../state/appView';
import { BrandMarkIcon, ChevronLeftIcon, SettingsIcon } from './icons/Icons';
import { UpdateNotice } from './UpdateNotice';

export function AppHeader() {
  const view = useAppView();
  const inSettings = view === 'settings';

  return (
    <header className="app-header">
      <div className="brand">
        <span className="brand-mark">
          <BrandMarkIcon />
        </span>
        <span>TermForge</span>
        {inSettings ? (
          <>
            <span className="brand-separator">/</span>
            <span className="brand-page">Settings</span>
          </>
        ) : null}
      </div>
      <div className="app-header-spacer" />
      {inSettings ? (
        <button type="button" className="btn btn-sm" onClick={() => showAppView('workspace')}>
          <ChevronLeftIcon />
          Back to workspace
        </button>
      ) : (
        <>
          <UpdateNotice />
          <button
            type="button"
            className="icon-btn icon-btn-lg"
            aria-label="Settings"
            onClick={() => showAppView('settings')}
          >
            <SettingsIcon />
          </button>
        </>
      )}
    </header>
  );
}
