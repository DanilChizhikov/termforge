import { useCallback, useState } from 'react';
import { showAppView, useAppView } from '../state/appView';
import { ExitDialog } from './ExitDialog';
import { BrandMarkIcon, ChevronLeftIcon, SettingsIcon } from './icons/Icons';
import { TransfersIndicator } from './TransfersIndicator';
import { UpdateNotice } from './UpdateNotice';

export function AppHeader() {
  const view = useAppView();
  const inSettings = view === 'settings';
  const [exiting, setExiting] = useState(false);
  const closeExit = useCallback(() => setExiting(false), []);
  const showWorkspace = () => showAppView('workspace');
  const brandName = (
    <>
      <span className="brand-mark">
        <BrandMarkIcon />
      </span>
      <span>TermForge</span>
    </>
  );

  return (
    <header className="app-header">
      <div className="brand">
        {inSettings ? (
          <button type="button" className="brand-home" title="Back to workspace" onClick={showWorkspace}>
            {brandName}
          </button>
        ) : (
          brandName
        )}
        {inSettings ? (
          <>
            <span className="brand-separator">/</span>
            <span className="brand-page">Settings</span>
          </>
        ) : null}
      </div>
      <div className="app-header-spacer" />
      {inSettings ? (
        <button type="button" className="btn btn-sm btn-header" onClick={showWorkspace}>
          <ChevronLeftIcon />
          Back to workspace
        </button>
      ) : (
        <>
          <UpdateNotice />
          <TransfersIndicator />
          <button
            type="button"
            className="icon-btn icon-btn-lg"
            aria-label="Settings"
            onClick={() => showAppView('settings')}
          >
            <SettingsIcon />
          </button>
          <button type="button" className="btn btn-sm" onClick={() => setExiting(true)}>
            Exit app
          </button>
        </>
      )}
      {exiting ? <ExitDialog onClose={closeExit} /> : null}
    </header>
  );
}
