import { useState, type ChangeEvent, type KeyboardEvent } from 'react';
import { describeError } from '../../api/connections';
import {
  applyImport,
  encodeBase64,
  previewImport,
  type ConflictResolution,
  type ConnectionImportStep,
  type ImportMode,
  type SettingsImportPreview,
  type SettingsImportRequest,
  type SettingsImportResult,
  type SettingsSections,
} from '../../api/settingsTransfer';
import { reloadConnections } from '../../state/connections';
import { flushWorkspaceState, pauseWorkspaceAutosave } from '../../state/workspacePersistence';
import { formatDayAndClock } from '../timeFormat';
import { CloseIcon } from '../icons/Icons';
import { readFileBytes, readFileText } from './transferFiles';

export interface ImportSettingsDialogProps {
  onClose: () => void;
  reloadPage?: () => void;
}

interface ChosenSettingsFile {
  name: string;
  text: string;
}

interface ChosenArchive {
  name: string;
  base64: string;
}

const ALL_SECTIONS: SettingsSections = { connections: true, preferences: true, workspace: true, pinnedCommands: true };

const SECTION_LABELS: Record<keyof SettingsSections, string> = {
  connections: 'Server connections',
  preferences: 'Preferences',
  workspace: 'Workspace layout and open tabs',
  pinnedCommands: 'Pinned commands',
};

const SECTION_KEYS: Array<keyof SettingsSections> = ['connections', 'preferences', 'workspace', 'pinnedCommands'];

const RESOLUTION_LABELS: Record<ConflictResolution, string> = {
  keepExisting: 'Keep mine',
  useImported: 'Use imported',
  keepBoth: 'Keep both',
};

function reloadBrowserPage(): void {
  window.location.reload();
}

function plural(count: number, one: string, many: string): string {
  return `${count} ${count === 1 ? one : many}`;
}

function actionBadge(step: ConnectionImportStep): { className: string; label: string } {
  switch (step.action) {
    case 'add':
      return { className: 'badge badge-info', label: 'New' };
    case 'update':
      return { className: 'badge badge-caution', label: 'Update' };
    case 'conflict':
      return { className: 'badge badge-caution', label: 'Conflict' };
    case 'unchanged':
      return { className: 'badge', label: 'Unchanged' };
    default:
      return { className: 'badge', label: 'Not imported' };
  }
}

function describeResult(result: SettingsImportResult): string[] {
  const lines: string[] = [];
  const servers = [
    result.added > 0 ? `${result.added} added` : null,
    result.updated > 0 ? `${result.updated} updated` : null,
    result.unchanged > 0 ? `${result.unchanged} unchanged` : null,
    result.kept > 0 ? `${result.kept} kept as they were` : null,
    result.removed > 0 ? `${result.removed} removed` : null,
  ].filter((part): part is string => part !== null);
  if (servers.length > 0) {
    lines.push(`Servers: ${servers.join(', ')}.`);
  }
  if (result.secretsStored > 0) {
    lines.push(`${plural(result.secretsStored, 'password', 'passwords')} saved to the credential store.`);
  }
  if (result.preferencesImported) {
    lines.push('Preferences imported.');
  }
  if (result.workspaceImported) {
    lines.push('Workspace layout imported.');
  }
  if (result.pinnedCommandsAdded > 0 || result.pinnedCommandsSkipped > 0) {
    lines.push(`Pinned commands: ${result.pinnedCommandsAdded} added, ${result.pinnedCommandsSkipped} skipped.`);
  }
  return lines;
}

export function ImportSettingsDialog({ onClose, reloadPage = reloadBrowserPage }: ImportSettingsDialogProps) {
  const [settingsFile, setSettingsFile] = useState<ChosenSettingsFile | null>(null);
  const [archive, setArchive] = useState<ChosenArchive | null>(null);
  const [password, setPassword] = useState('');
  const [mode, setMode] = useState<ImportMode>('merge');
  const [sections, setSections] = useState<SettingsSections>(ALL_SECTIONS);
  const [preview, setPreview] = useState<SettingsImportPreview | null>(null);
  const [resolutions, setResolutions] = useState<Record<string, ConflictResolution>>({});
  const [confirmReplace, setConfirmReplace] = useState(false);
  const [result, setResult] = useState<SettingsImportResult | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const resetPreview = () => {
    setPreview(null);
    setResolutions({});
    setConfirmReplace(false);
    setError(null);
  };

  const chooseSettingsFile = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    resetPreview();
    setSections(ALL_SECTIONS);
    if (file === undefined) {
      setSettingsFile(null);
      return;
    }
    try {
      const text = await readFileText(file);
      setSettingsFile({ name: file.name, text });
    } catch {
      setSettingsFile(null);
      setError('The settings file could not be read.');
    }
  };

  const chooseArchive = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    resetPreview();
    if (file === undefined) {
      setArchive(null);
      setPassword('');
      return;
    }
    try {
      const bytes = await readFileBytes(file);
      setArchive({ name: file.name, base64: encodeBase64(bytes) });
    } catch {
      setArchive(null);
      setError('The secrets archive could not be read.');
    }
  };

  const buildRequest = (requestSections: SettingsSections, requestMode: ImportMode): SettingsImportRequest | null => {
    if (settingsFile === null) {
      return null;
    }
    return {
      settingsFile: settingsFile.text,
      secretsArchive: archive?.base64 ?? null,
      password: archive === null ? null : password,
      mode: requestMode,
      sections: requestSections,
      resolutions,
      confirmReplace,
    };
  };

  const runPreview = async (requestSections: SettingsSections, requestMode: ImportMode) => {
    const request = buildRequest(requestSections, requestMode);
    if (request === null) {
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const loaded = await previewImport(request);
      setPreview(loaded);
      setSections(loaded.sectionsToApply);
      setConfirmReplace(false);
    } catch (failure: unknown) {
      setPreview(null);
      setError(describeError(failure));
    } finally {
      setBusy(false);
    }
  };

  const changeMode = (next: ImportMode) => {
    setMode(next);
    setConfirmReplace(false);
    if (preview !== null) {
      void runPreview(sections, next);
    }
  };

  const toggleSection = (section: keyof SettingsSections, checked: boolean) => {
    const next = { ...sections, [section]: checked };
    setSections(next);
    void runPreview(next, mode);
  };

  const runImport = async () => {
    const request = buildRequest(sections, mode);
    if (request === null) {
      return;
    }
    setBusy(true);
    setError(null);
    try {
      if (sections.workspace) {
        await flushWorkspaceState();
      }
      const imported = await applyImport(request);
      if (imported.workspaceImported) {
        pauseWorkspaceAutosave();
      }
      setPassword('');
      setResult(imported);
      await reloadConnections().catch(() => undefined);
    } catch (failure: unknown) {
      setError(describeError(failure));
    } finally {
      setBusy(false);
    }
  };

  const closeOnEscape = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape' && !busy && result?.workspaceImported !== true) {
      onClose();
    }
  };

  const removals = preview?.removals ?? [];
  const needsReplaceConfirmation = mode === 'replace' && removals.length > 0;
  const nothingSelected = !SECTION_KEYS.some((key) => sections[key]);
  const canImport = preview !== null && !busy && !nothingSelected && (!needsReplaceConfirmation || confirmReplace);
  const canPreview = settingsFile !== null && !busy && (archive === null || password.length > 0);
  const visibleSteps = preview?.connections.filter((step) => step.action !== 'skipped') ?? [];

  return (
    <div className="dialog-backdrop">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="import-settings-title"
        className="dialog-card transfer-dialog"
        onKeyDown={closeOnEscape}
      >
        <div className="dialog-header">
          <h2 id="import-settings-title" className="dialog-title">
            Import settings
          </h2>
          {result?.workspaceImported === true ? null : (
            <button type="button" className="icon-btn" aria-label="Close" onClick={onClose} disabled={busy}>
              <CloseIcon size={16} />
            </button>
          )}
        </div>

        {result === null ? (
          <>
            <p className="transfer-dialog-lead">Restore a TermForge settings file. Nothing changes until you import.</p>
            <div className="transfer-body">
              <div className="transfer-password-grid">
                <label className="field">
                  <span className="field-label">Settings file</span>
                  <input type="file" className="transfer-file-input" accept=".json,application/json" onChange={(event) => void chooseSettingsFile(event)} />
                </label>
                <label className="field">
                  <span className="field-label">Secrets archive (optional)</span>
                  <input type="file" className="transfer-file-input" accept=".tfvault" onChange={(event) => void chooseArchive(event)} />
                </label>
              </div>
              {archive === null ? null : (
                <label className="field">
                  <span className="field-label">Archive password</span>
                  <input
                    type="password"
                    className="input"
                    autoComplete="off"
                    value={password}
                    onChange={(event) => {
                      setPassword(event.target.value);
                      resetPreview();
                    }}
                  />
                </label>
              )}
              <fieldset className="auth-options">
                <legend className="field-label">When a server already exists</legend>
                <label className="auth-option">
                  <input type="radio" name="import-mode" checked={mode === 'merge'} onChange={() => changeMode('merge')} />
                  <span className="auth-option-title">Merge with my settings</span>
                  <span className="auth-option-hint">Add new servers, choose per conflict. Nothing is removed.</span>
                </label>
                <label className="auth-option">
                  <input type="radio" name="import-mode" checked={mode === 'replace'} onChange={() => changeMode('replace')} />
                  <span className="auth-option-title">Replace my settings</span>
                  <span className="auth-option-hint">The file wins. Servers missing from it are removed.</span>
                </label>
              </fieldset>

              {preview === null ? null : (
                <ImportPreview
                  preview={preview}
                  sections={sections}
                  visibleSteps={visibleSteps}
                  resolutions={resolutions}
                  busy={busy}
                  onToggleSection={toggleSection}
                  onResolve={(importedId, resolution) => setResolutions({ ...resolutions, [importedId]: resolution })}
                />
              )}

              {needsReplaceConfirmation ? (
                <label className="check-row">
                  <input
                    type="checkbox"
                    className="checkbox"
                    checked={confirmReplace}
                    onChange={(event) => setConfirmReplace(event.target.checked)}
                  />
                  Remove {plural(removals.length, 'server', 'servers')} that {removals.length === 1 ? 'is' : 'are'} not in
                  the file, with {removals.length === 1 ? 'its' : 'their'} saved passwords.
                </label>
              ) : null}

              {error === null ? null : (
                <p role="alert" className="form-error">
                  {error}
                </p>
              )}
            </div>
            <div className="dialog-footer dialog-footer-plain">
              <button type="button" className="btn" onClick={onClose} disabled={busy}>
                Cancel
              </button>
              {preview === null ? (
                <button type="button" className="btn btn-primary" disabled={!canPreview} onClick={() => void runPreview(sections, mode)}>
                  {busy ? 'Checking…' : 'Preview import'}
                </button>
              ) : (
                <button
                  type="button"
                  className={needsReplaceConfirmation ? 'btn btn-destructive' : 'btn btn-primary'}
                  disabled={!canImport}
                  onClick={() => void runImport()}
                >
                  {busy ? 'Importing…' : mode === 'replace' ? 'Replace settings' : 'Import'}
                </button>
              )}
            </div>
          </>
        ) : (
          <>
            <div className="transfer-body">
              <ul className="transfer-result" aria-label="Import result">
                {describeResult(result).map((line) => (
                  <li key={line}>{line}</li>
                ))}
              </ul>
              {result.warnings.length > 0 ? (
                <ul className="notice notice-caution transfer-warnings" aria-label="Import warnings">
                  {result.warnings.map((warning) => (
                    <li key={warning}>{warning}</li>
                  ))}
                </ul>
              ) : null}
              {result.workspaceImported ? (
                <p className="text-secondary">Reload TermForge to open the imported workspace. Layout changes are not saved until then.</p>
              ) : null}
            </div>
            <div className="dialog-footer dialog-footer-plain">
              {result.workspaceImported ? (
                <button type="button" className="btn btn-primary" onClick={reloadPage}>
                  Reload TermForge
                </button>
              ) : (
                <button type="button" className="btn btn-primary" onClick={onClose}>
                  Done
                </button>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  );
}

interface ImportPreviewProps {
  preview: SettingsImportPreview;
  sections: SettingsSections;
  visibleSteps: ConnectionImportStep[];
  resolutions: Record<string, ConflictResolution>;
  busy: boolean;
  onToggleSection: (section: keyof SettingsSections, checked: boolean) => void;
  onResolve: (importedId: string, resolution: ConflictResolution) => void;
}

function ImportPreview({ preview, sections, visibleSteps, resolutions, busy, onToggleSection, onResolve }: ImportPreviewProps) {
  const availableSections = SECTION_KEYS.filter((key) => preview.sectionsInFile[key]);
  return (
    <section className="transfer-preview" aria-label="Import preview">
      <span className="transfer-files-label">Exported {formatDayAndClock(preview.exportedAt)}</span>
      <fieldset className="transfer-sections">
        <legend>Import from this file</legend>
        {availableSections.map((key) => (
          <label key={key} className="transfer-option">
            <input
              type="checkbox"
              className="checkbox"
              checked={sections[key]}
              disabled={busy}
              onChange={(event) => onToggleSection(key, event.target.checked)}
            />
            <span className="transfer-option-label">{SECTION_LABELS[key]}</span>
            <span className="transfer-option-hint">{sectionHint(key, preview)}</span>
          </label>
        ))}
      </fieldset>

      {visibleSteps.length > 0 || preview.removals.length > 0 ? (
        <ul className="transfer-preview-list" aria-label="Server changes">
          {visibleSteps.map((step) => {
            const badge = actionBadge(step);
            const resolution = resolutions[step.importedId] ?? step.resolution ?? 'keepExisting';
            return (
              <li key={step.importedId} className="transfer-preview-row">
                <span className="transfer-preview-text">
                  <span className="transfer-preview-name">{step.name}</span>
                  <code className="transfer-preview-host">
                    {step.userName}@{step.host}:{step.port}
                  </code>
                  {step.action === 'conflict' && step.changes.length > 0 ? (
                    <span className="transfer-option-hint">Differs: {step.changes.join(', ')}</span>
                  ) : null}
                </span>
                <span className={badge.className}>{badge.label}</span>
                {step.action === 'conflict' ? (
                  <select
                    className="input input-sm transfer-resolution"
                    aria-label={`Resolve ${step.name}`}
                    value={resolution}
                    disabled={busy}
                    onChange={(event) => onResolve(step.importedId, event.target.value as ConflictResolution)}
                  >
                    {(Object.keys(RESOLUTION_LABELS) as ConflictResolution[]).map((option) => (
                      <option key={option} value={option}>
                        {RESOLUTION_LABELS[option]}
                      </option>
                    ))}
                  </select>
                ) : null}
              </li>
            );
          })}
          {preview.removals.map((removal) => (
            <li key={removal.id} className="transfer-preview-row">
              <span className="transfer-preview-text">
                <span className="transfer-preview-name">{removal.name}</span>
                <code className="transfer-preview-host">{removal.host}</code>
              </span>
              <span className="badge badge-danger">Remove</span>
            </li>
          ))}
        </ul>
      ) : null}

      {preview.preferenceChanges.length > 0 && sections.preferences ? (
        <ul className="transfer-result" aria-label="Preference changes">
          {preview.preferenceChanges.map((change) => (
            <li key={change}>{change}</li>
          ))}
        </ul>
      ) : null}

      {preview.warnings.length > 0 ? (
        <ul className="notice notice-caution transfer-warnings" aria-label="Import warnings">
          {preview.warnings.map((warning) => (
            <li key={warning}>{warning}</li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}

function sectionHint(key: keyof SettingsSections, preview: SettingsImportPreview): string {
  switch (key) {
    case 'connections': {
      const passwords = preview.secrets.supplied ? ` · ${plural(preview.secrets.passwords + preview.secrets.sudoPasswords, 'password', 'passwords')}` : '';
      return `${plural(preview.connections.length, 'server', 'servers')}${passwords}`;
    }
    case 'preferences':
      return preview.preferenceChanges.length === 0 ? 'no changes' : plural(preview.preferenceChanges.length, 'change', 'changes');
    case 'workspace':
      return preview.workspace === null
        ? 'replaces the current layout'
        : `replaces the layout · ${plural(preview.workspace.terminalTabs, 'terminal tab', 'terminal tabs')}`;
    default:
      return plural(preview.pinnedCommands, 'command', 'commands');
  }
}
