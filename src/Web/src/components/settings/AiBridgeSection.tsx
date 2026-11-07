import { useEffect, useState } from 'react';
import {
  getAiBridgeSettings,
  updateAiBridgeSettings,
  type AiBridgeSettings,
} from '../../api/aiBridge';
import { describeError } from '../../api/connections';
import { LoadingState } from '../LoadingState';
import { Switch } from '../Switch';
import { AiBridgeTokens } from './AiBridgeTokens';

export function AiBridgeSection() {
  const [settings, setSettings] = useState<AiBridgeSettings | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    getAiBridgeSettings()
      .then((loaded) => {
        if (active) setSettings(loaded);
      })
      .catch((failure: unknown) => {
        if (active) setError(describeError(failure));
      });
    return () => {
      active = false;
    };
  }, []);

  const save = (next: Pick<AiBridgeSettings, 'enabled' | 'requireApproval'>) => {
    setSaving(true);
    setError(null);
    updateAiBridgeSettings(next)
      .then(setSettings)
      .catch((failure: unknown) => setError(describeError(failure)))
      .finally(() => setSaving(false));
  };

  return (
    <section className="settings-section" aria-labelledby="settings-ai-bridge-title">
      <div className="settings-section-header">
        <div className="settings-row-text">
          <h2 id="settings-ai-bridge-title" className="settings-section-title">
            AI bridge
          </h2>
          <span className="settings-row-hint">
            An MCP server for AI agents such as Claude Code. It works only with servers that have AI access turned on.
          </span>
        </div>
      </div>
      <div className="settings-card">
        {settings === null ? (
          error === null ? (
            <LoadingState message="Loading AI bridge settings…" className="settings-row" />
          ) : null
        ) : (
          <>
            <div className="settings-row">
              <span className="settings-row-text">
                <span className="settings-row-title">Enable AI bridge</span>
                <span className="settings-row-hint">Off by default. Accepts connections from this computer only.</span>
              </span>
              <Switch
                label="Enable AI bridge"
                checked={settings.enabled}
                disabled={saving}
                onChange={(checked) => save({ enabled: checked, requireApproval: settings.requireApproval })}
              />
            </div>
            <div className="settings-row">
              <span className="settings-row-text">
                <span className="settings-row-title">Ask before AI actions</span>
                <span className="settings-row-hint">
                  Commands, terminal input, Docker actions and file writes wait for your approval. Reading never asks.
                </span>
              </span>
              <Switch
                label="Ask before AI actions"
                checked={settings.requireApproval}
                disabled={saving}
                onChange={(checked) => save({ enabled: settings.enabled, requireApproval: checked })}
              />
            </div>
            <div className="settings-row">
              <span className="settings-row-text">
                <span className="settings-row-title">Endpoint</span>
                <code className="mono settings-row-hint">{settings.mcpUrl}</code>
              </span>
            </div>
            <AiBridgeTokens />
          </>
        )}
        {error === null ? null : (
          <p role="alert" className="settings-row form-error">
            {error}
          </p>
        )}
      </div>
    </section>
  );
}
