import { useEffect, useState, type KeyboardEvent } from 'react';
import { describeError } from '../../api/connections';
import {
  ARCHIVE_PASSWORD_MIN_LENGTH,
  decodeBase64,
  exportSettings,
  getTransferSummary,
  type SettingsSections,
  type SettingsTransferSummary,
} from '../../api/settingsTransfer';
import { flushWorkspaceState } from '../../state/workspacePersistence';
import { CloseIcon, FileIcon, LockIcon } from '../icons/Icons';
import { Switch } from '../Switch';
import { measurePassword } from './passwordStrength';
import { downloadFile, exportDateStamp, type SaveFile } from './transferFiles';

export interface ExportSettingsDialogProps {
  onClose: () => void;
  onExported: (fileNames: string[]) => void;
  saveFile?: SaveFile;
}

const DEFAULT_SECTIONS: SettingsSections = {
  connections: true,
  preferences: true,
  workspace: true,
  pinnedCommands: false,
};

function plural(count: number, one: string, many: string): string {
  return `${count} ${count === 1 ? one : many}`;
}

function connectionsHint(summary: SettingsTransferSummary | null): string {
  if (summary === null) {
    return 'saved servers';
  }
  return `${plural(summary.connections, 'server', 'servers')} · ${plural(summary.groups, 'group', 'groups')}`;
}

function pinnedHint(summary: SettingsTransferSummary | null): string {
  return summary === null ? 'pinned in command history' : plural(summary.pinnedCommands, 'command', 'commands');
}

export function ExportSettingsDialog({ onClose, onExported, saveFile = downloadFile }: ExportSettingsDialogProps) {
  const [sections, setSections] = useState<SettingsSections>(DEFAULT_SECTIONS);
  const [summary, setSummary] = useState<SettingsTransferSummary | null>(null);
  const [includeSecrets, setIncludeSecrets] = useState(false);
  const [password, setPassword] = useState('');
  const [repeatedPassword, setRepeatedPassword] = useState('');
  const [confirmed, setConfirmed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    getTransferSummary()
      .then((loaded) => {
        if (active) {
          setSummary(loaded);
        }
      })
      .catch(() => undefined);
    return () => {
      active = false;
    };
  }, []);

  const toggleSection = (section: keyof SettingsSections, checked: boolean) => {
    setSections({ ...sections, [section]: checked });
    if (section === 'connections' && !checked) {
      setIncludeSecrets(false);
    }
  };

  const toggleSecrets = (checked: boolean) => {
    setIncludeSecrets(checked);
    if (!checked) {
      setPassword('');
      setRepeatedPassword('');
      setConfirmed(false);
    }
  };

  const strength = measurePassword(password, ARCHIVE_PASSWORD_MIN_LENGTH);
  const passwordsMatch = password.length > 0 && password === repeatedPassword;
  const secretsReady = !includeSecrets || (strength.score > 0 && passwordsMatch && confirmed);
  const anySection = sections.connections || sections.preferences || sections.workspace || sections.pinnedCommands;
  const canExport = anySection && secretsReady && !busy;
  const stamp = exportDateStamp(new Date());
  const fileCount = includeSecrets ? 2 : 1;

  const runExport = async () => {
    setBusy(true);
    setError(null);
    try {
      if (sections.workspace) {
        await flushWorkspaceState();
      }
      const secrets = includeSecrets ? { password, confirmed } : null;
      const exported = await exportSettings({ sections, secrets });
      saveFile(exported.settings.fileName, exported.settings.content, 'application/json');
      const fileNames = [exported.settings.fileName];
      if (exported.secrets !== null) {
        const archive = decodeBase64(exported.secrets.contentBase64);
        saveFile(exported.secrets.fileName, archive, 'application/octet-stream');
        fileNames.push(exported.secrets.fileName);
      }
      setPassword('');
      setRepeatedPassword('');
      onExported(fileNames);
    } catch (failure: unknown) {
      setError(describeError(failure));
    } finally {
      setBusy(false);
    }
  };

  const closeOnEscape = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape' && !busy) {
      onClose();
    }
  };

  const strengthTone = strength.score >= 3 ? 'strong' : strength.score === 2 ? 'fair' : 'weak';
  const strengthText = passwordsMatch ? `${strength.label} · passwords match` : repeatedPassword.length > 0 ? `${strength.label} · passwords differ` : strength.label;

  return (
    <div className="dialog-backdrop">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="export-settings-title"
        className="dialog-card transfer-dialog"
        onKeyDown={closeOnEscape}
      >
        <div className="dialog-header">
          <h2 id="export-settings-title" className="dialog-title">
            Export settings
          </h2>
          <button type="button" className="icon-btn" aria-label="Close" onClick={onClose} disabled={busy}>
            <CloseIcon size={16} />
          </button>
        </div>
        <p className="transfer-dialog-lead">Move TermForge to another computer, or keep a backup.</p>

        <fieldset className="transfer-sections">
          <legend>Include in settings file</legend>
          <label className="transfer-option">
            <input
              type="checkbox"
              className="checkbox"
              checked={sections.connections}
              onChange={(event) => toggleSection('connections', event.target.checked)}
            />
            <span className="transfer-option-label">Server connections</span>
            <span className="transfer-option-hint">{connectionsHint(summary)}</span>
          </label>
          <label className="transfer-option">
            <input
              type="checkbox"
              className="checkbox"
              checked={sections.preferences}
              onChange={(event) => toggleSection('preferences', event.target.checked)}
            />
            <span className="transfer-option-label">Preferences</span>
            <span className="transfer-option-hint">recording limits, history filters</span>
          </label>
          <label className="transfer-option">
            <input
              type="checkbox"
              className="checkbox"
              checked={sections.workspace}
              onChange={(event) => toggleSection('workspace', event.target.checked)}
            />
            <span className="transfer-option-label">Workspace layout and open tabs</span>
            <span className="transfer-option-hint">paths, panel sizes</span>
          </label>
          <label className="transfer-option">
            <input
              type="checkbox"
              className="checkbox"
              checked={sections.pinnedCommands}
              onChange={(event) => toggleSection('pinnedCommands', event.target.checked)}
            />
            <span className="transfer-option-label">Pinned commands</span>
            <span className="transfer-option-hint">{pinnedHint(summary)}</span>
          </label>
        </fieldset>

        <div className="transfer-secrets">
          <div className="transfer-secrets-header">
            <span className="transfer-secrets-text">
              <span className="transfer-secrets-title">Also export secrets</span>
              <span className="transfer-secrets-hint">
                {sections.connections
                  ? 'Passwords and tokens go into a separate encrypted archive. Private keys are never exported.'
                  : 'Include server connections to export their passwords.'}
              </span>
            </span>
            <Switch
              label="Also export secrets"
              checked={includeSecrets}
              disabled={!sections.connections || busy}
              onChange={toggleSecrets}
            />
          </div>
          {includeSecrets ? (
            <>
              <div className="transfer-password-grid">
                <label className="field">
                  <span className="field-label">Archive password</span>
                  <input
                    type="password"
                    className="input"
                    autoComplete="new-password"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                  />
                </label>
                <label className="field">
                  <span className="field-label">Repeat password</span>
                  <input
                    type="password"
                    className="input"
                    autoComplete="new-password"
                    value={repeatedPassword}
                    onChange={(event) => setRepeatedPassword(event.target.value)}
                  />
                </label>
              </div>
              <div className="strength">
                <div className="strength-bars" aria-hidden="true">
                  {[1, 2, 3, 4].map((segment) => (
                    <span
                      key={segment}
                      className={segment <= strength.score ? `strength-bar strength-bar-${strengthTone}` : 'strength-bar'}
                    />
                  ))}
                </div>
                <span className={`strength-label strength-label-${passwordsMatch && strength.score > 0 ? strengthTone : 'weak'}`} data-testid="password-strength">
                  {password.length === 0 ? `At least ${ARCHIVE_PASSWORD_MIN_LENGTH} characters` : strengthText}
                </span>
              </div>
              <label className="check-row">
                <input
                  type="checkbox"
                  className="checkbox"
                  checked={confirmed}
                  onChange={(event) => setConfirmed(event.target.checked)}
                />
                I understand that anyone with this archive and its password can sign in to my servers. TermForge can’t
                recover a lost password.
              </label>
            </>
          ) : null}
        </div>

        <div className="transfer-files">
          <span className="transfer-files-label">{fileCount === 2 ? 'You’ll get two files' : 'You’ll get one file'}</span>
          <div className="transfer-file">
            <FileIcon size={15} />
            <code className="transfer-file-name">termforge-settings-{stamp}.json</code>
            <span className="transfer-file-tag">no secrets</span>
          </div>
          {includeSecrets ? (
            <div className="transfer-file transfer-file-encrypted">
              <LockIcon size={15} />
              <code className="transfer-file-name">termforge-secrets-{stamp}.tfvault</code>
              <span className="transfer-file-tag">encrypted</span>
            </div>
          ) : null}
        </div>

        {error === null ? null : (
          <p role="alert" className="transfer-error form-error">
            {error}
          </p>
        )}

        <div className="dialog-footer dialog-footer-plain">
          <button type="button" className="btn" onClick={onClose} disabled={busy}>
            Cancel
          </button>
          <button type="button" className="btn btn-primary" disabled={!canExport} onClick={() => void runExport()}>
            {busy ? 'Exporting…' : fileCount === 2 ? 'Export 2 files' : 'Export 1 file'}
          </button>
        </div>
      </div>
    </div>
  );
}
