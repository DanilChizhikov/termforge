import { useEffect, useState } from 'react';
import { describeError } from '../../api/connections';
import {
  getRecordingSettings,
  RECORDING_LIMITS,
  updateRecordingSettings,
  type RecordingSettings,
  type RecordingSettingsResponse,
} from '../../api/recordings';
import { LoadingState } from '../LoadingState';
import { RecordingWarningDialog } from '../RecordingWarningDialog';
import { Switch } from '../Switch';

export interface RecordingSettingsSectionProps {
  onChanged: () => void;
}

interface LimitDraft {
  maxRecordingSizeMegabytes: string;
  maxRecordings: string;
  retentionDays: string;
}

function draftFrom(settings: RecordingSettings): LimitDraft {
  return {
    maxRecordingSizeMegabytes: String(settings.maxRecordingSizeMegabytes),
    maxRecordings: String(settings.maxRecordings),
    retentionDays: String(settings.retentionDays),
  };
}

function limitsOf(settings: RecordingSettings): Omit<RecordingSettings, 'enabled'> {
  return {
    maxRecordingSizeMegabytes: settings.maxRecordingSizeMegabytes,
    maxRecordings: settings.maxRecordings,
    retentionDays: settings.retentionDays,
  };
}

function parseLimit(text: string, min: number, max: number): number | null {
  if (!/^\d+$/.test(text.trim())) {
    return null;
  }
  const value = Number(text);
  return value >= min && value <= max ? value : null;
}

function limitsFrom(draft: LimitDraft): Omit<RecordingSettings, 'enabled'> | null {
  const maxRecordingSizeMegabytes = parseLimit(
    draft.maxRecordingSizeMegabytes,
    RECORDING_LIMITS.minSizeMegabytes,
    RECORDING_LIMITS.maxSizeMegabytes,
  );
  const maxRecordings = parseLimit(draft.maxRecordings, RECORDING_LIMITS.minRecordings, RECORDING_LIMITS.maxRecordings);
  const retentionDays = parseLimit(draft.retentionDays, RECORDING_LIMITS.minRetentionDays, RECORDING_LIMITS.maxRetentionDays);
  if (maxRecordingSizeMegabytes === null || maxRecordings === null || retentionDays === null) {
    return null;
  }
  return { maxRecordingSizeMegabytes, maxRecordings, retentionDays };
}

export function RecordingSettingsSection({ onChanged }: RecordingSettingsSectionProps) {
  const [settings, setSettings] = useState<RecordingSettingsResponse | null>(null);
  const [draft, setDraft] = useState<LimitDraft | null>(null);
  const [confirmingEnable, setConfirmingEnable] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    getRecordingSettings()
      .then((loaded) => {
        if (active) {
          setSettings(loaded);
          setDraft(draftFrom(loaded));
        }
      })
      .catch((failure: unknown) => {
        if (active) {
          setError(describeError(failure));
        }
      });
    return () => {
      active = false;
    };
  }, []);

  const save = (next: RecordingSettings) => {
    setSaving(true);
    setError(null);
    updateRecordingSettings(next)
      .then((saved) => {
        setSettings(saved);
        setDraft(draftFrom(saved));
        onChanged();
      })
      .catch((failure: unknown) => setError(describeError(failure)))
      .finally(() => {
        setSaving(false);
        setConfirmingEnable(false);
      });
  };

  if (settings === null || draft === null) {
    return (
      <section className="settings-card" aria-label="Terminal recording">
        {error === null ? (
          <LoadingState message="Loading recording settings…" className="settings-row" />
        ) : (
          <p role="alert" className="settings-row form-error">
            {error}
          </p>
        )}
      </section>
    );
  }

  const limits = limitsFrom(draft);
  const isDirty =
    draft.maxRecordingSizeMegabytes !== String(settings.maxRecordingSizeMegabytes) ||
    draft.maxRecordings !== String(settings.maxRecordings) ||
    draft.retentionDays !== String(settings.retentionDays);

  const toggleRecording = (checked: boolean) => {
    if (checked) {
      setConfirmingEnable(true);
      return;
    }
    save({ ...limitsOf(settings), enabled: false });
  };

  const saveLimits = () => {
    if (limits !== null) {
      save({ enabled: settings.enabled, ...limits });
    }
  };

  const updateDraft = (field: keyof LimitDraft, value: string) => setDraft({ ...draft, [field]: value });

  return (
    <section className="settings-card" aria-label="Terminal recording">
      <div className="settings-row">
        <span className="settings-row-text">
          <span className="settings-row-title">Record terminal output</span>
          <span className="settings-row-hint">
            Off by default. Command history keeps commands, times and exit codes only.
          </span>
        </span>
        <Switch
          label="Record terminal output"
          checked={settings.enabled}
          disabled={saving}
          onChange={toggleRecording}
        />
      </div>
      <label className="settings-row">
        <span className="settings-row-text">
          <span className="settings-row-title">Maximum size per recording</span>
          <span className="settings-row-hint">A recording stops and is marked truncated when it reaches this size.</span>
        </span>
        <span className="settings-unit-field">
          <input
            className="input input-sm settings-number"
            inputMode="numeric"
            aria-label="Maximum size per recording in MB"
            value={draft.maxRecordingSizeMegabytes}
            onChange={(event) => updateDraft('maxRecordingSizeMegabytes', event.target.value)}
          />
          <span className="text-secondary">MB</span>
        </span>
      </label>
      <label className="settings-row">
        <span className="settings-row-text">
          <span className="settings-row-title">Keep at most</span>
          <span className="settings-row-hint">The oldest recordings are deleted first.</span>
        </span>
        <span className="settings-unit-field">
          <input
            className="input input-sm settings-number"
            inputMode="numeric"
            aria-label="Maximum number of recordings"
            value={draft.maxRecordings}
            onChange={(event) => updateDraft('maxRecordings', event.target.value)}
          />
          <span className="text-secondary">recordings</span>
        </span>
      </label>
      <label className="settings-row">
        <span className="settings-row-text">
          <span className="settings-row-title">Delete recordings after</span>
          <span className="settings-row-hint">Recordings in progress are never deleted.</span>
        </span>
        <span className="settings-unit-field">
          <input
            className="input input-sm settings-number"
            inputMode="numeric"
            aria-label="Days to keep recordings"
            value={draft.retentionDays}
            onChange={(event) => updateDraft('retentionDays', event.target.value)}
          />
          <span className="text-secondary">days</span>
        </span>
      </label>
      <div className="settings-row settings-row-footer">
        <span className="settings-row-hint">
          Stored in <code className="mono">{settings.directory}</code>
        </span>
        {limits === null ? (
          <span className="form-error">
            Use {RECORDING_LIMITS.minSizeMegabytes}–{RECORDING_LIMITS.maxSizeMegabytes} MB,{' '}
            {RECORDING_LIMITS.minRecordings}–{RECORDING_LIMITS.maxRecordings} recordings and{' '}
            {RECORDING_LIMITS.minRetentionDays}–{RECORDING_LIMITS.maxRetentionDays} days.
          </span>
        ) : null}
        <button type="button" className="btn btn-sm" disabled={!isDirty || limits === null || saving} onClick={saveLimits}>
          Save limits
        </button>
      </div>
      {error === null ? null : (
        <p role="alert" className="settings-row form-error">
          {error}
        </p>
      )}
      {confirmingEnable ? (
        <RecordingWarningDialog
          title="Turn on terminal recording?"
          confirmLabel="Turn on recording"
          busy={saving}
          onConfirm={() => save({ ...limitsOf(settings), enabled: true })}
          onCancel={() => setConfirmingEnable(false)}
        />
      ) : null}
    </section>
  );
}
