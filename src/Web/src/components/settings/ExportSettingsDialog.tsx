import { useEffect, useState, type KeyboardEvent } from 'react';
import { describeError } from '../../api/connections';
import {
  SETTINGS_FILE_EXTENSION,
  decodeBase64,
  exportSettings,
  getTransferSummary,
  type SettingsSections,
  type SettingsTransferSummary,
} from '../../api/settingsTransfer';
import { flushWorkspaceState } from '../../state/workspacePersistence';
import { CloseIcon, LockIcon } from '../icons/Icons';
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

interface ExportedPackage {
  fileName: string;
  key: string;
}

function copyToClipboard(text: string): Promise<void> {
  return navigator.clipboard?.writeText(text) ?? Promise.reject(new Error('The clipboard is not available.'));
}

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
  const [exported, setExported] = useState<ExportedPackage | null>(null);
  const [copied, setCopied] = useState(false);
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
  };

  const anySection = sections.connections || sections.preferences || sections.workspace || sections.pinnedCommands;
  const canExport = anySection && !busy;
  const stamp = exportDateStamp(new Date());

  const runExport = async () => {
    setBusy(true);
    setError(null);
    try {
      if (sections.workspace) {
        await flushWorkspaceState();
      }
      const result = await exportSettings({ sections });
      const content = decodeBase64(result.file.contentBase64);
      saveFile(result.file.fileName, content, 'application/octet-stream');
      setExported({ fileName: result.file.fileName, key: result.key });
    } catch (failure: unknown) {
      setError(describeError(failure));
    } finally {
      setBusy(false);
    }
  };

  const copyKey = async (key: string) => {
    setError(null);
    try {
      await copyToClipboard(key);
      setCopied(true);
    } catch {
      setError('The key could not be copied. Select it and copy it manually.');
    }
  };

  const close = () => {
    if (exported === null) {
      onClose();
      return;
    }
    onExported([exported.fileName]);
  };

  const closeOnEscape = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape' && !busy) {
      close();
    }
  };

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
          <button type="button" className="icon-btn" aria-label="Close" onClick={close} disabled={busy}>
            <CloseIcon size={16} />
          </button>
        </div>
        {exported === null ? (
          <ExportForm
            sections={sections}
            summary={summary}
            stamp={stamp}
            onToggleSection={toggleSection}
          />
        ) : (
          <ExportKey exported={exported} copied={copied} onCopy={(key) => void copyKey(key)} />
        )}

        {error === null ? null : (
          <p role="alert" className="transfer-error form-error">
            {error}
          </p>
        )}

        <div className="dialog-footer dialog-footer-plain">
          {exported === null ? (
            <>
              <button type="button" className="btn" onClick={onClose} disabled={busy}>
                Cancel
              </button>
              <button type="button" className="btn btn-primary" disabled={!canExport} onClick={() => void runExport()}>
                {busy ? 'Exporting…' : 'Export'}
              </button>
            </>
          ) : (
            <button type="button" className="btn btn-primary" onClick={close}>
              Done
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

interface ExportFormProps {
  sections: SettingsSections;
  summary: SettingsTransferSummary | null;
  stamp: string;
  onToggleSection: (section: keyof SettingsSections, checked: boolean) => void;
}

function ExportForm({ sections, summary, stamp, onToggleSection: toggleSection }: ExportFormProps) {
  return (
    <>
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

      <div className="transfer-files">
        <span className="transfer-files-label">You’ll get one encrypted file and a key to open it</span>
        <div className="transfer-file transfer-file-encrypted">
          <LockIcon size={15} />
          <code className="transfer-file-name">
            termforge-settings-{stamp}
            {SETTINGS_FILE_EXTENSION}
          </code>
          <span className="transfer-file-tag">{sections.connections ? 'encrypted · with saved passwords' : 'encrypted'}</span>
        </div>
      </div>
    </>
  );
}

interface ExportKeyProps {
  exported: ExportedPackage;
  copied: boolean;
  onCopy: (key: string) => void;
}

function ExportKey({ exported, copied, onCopy }: ExportKeyProps) {
  return (
    <>
      <p className="transfer-dialog-lead">
        Saved <code className="mono">{exported.fileName}</code>. Import it on another computer with this key.
      </p>
      <div className="transfer-body">
        <p className="notice notice-caution">
          Copy the key now. Without it the file can’t be imported, and TermForge doesn’t keep a copy. Anyone with the
          file and the key can sign in to your servers.
        </p>
        <code className="mono transfer-key" aria-label="Export key">
          {exported.key}
        </code>
        <span>
          <button type="button" className="btn btn-sm" onClick={() => onCopy(exported.key)}>
            {copied ? 'Copied' : 'Copy key'}
          </button>
        </span>
      </div>
    </>
  );
}
