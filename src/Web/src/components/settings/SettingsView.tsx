import { useState } from 'react';
import { RecordingList } from './RecordingList';
import { RecordingSettingsSection } from './RecordingSettingsSection';
import { UpdatesCard } from './UpdatesCard';
import { UpdatesNavLink } from './UpdatesNavLink';

export function SettingsView() {
  const [version, setVersion] = useState(0);

  return (
    <div className="settings-view">
      <nav aria-label="Settings sections" className="settings-nav">
        <a href="#settings-security" aria-current="page" className="settings-nav-link">
          Security
        </a>
        <UpdatesNavLink />
      </nav>
      <main id="settings-security" className="settings-main">
        <div className="settings-content">
          <div className="settings-heading">
            <h1 className="page-title">Security</h1>
            <p className="text-secondary">TermForge runs under your OS user only. Defaults keep everything on this machine.</p>
          </div>
          <RecordingSettingsSection onChanged={() => setVersion((current) => current + 1)} />
          <RecordingList version={version} />
          <UpdatesCard />
        </div>
      </main>
    </div>
  );
}
