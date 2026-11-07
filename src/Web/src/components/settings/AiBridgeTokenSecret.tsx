import { useState } from 'react';
import { mcpAddCommand, type AiBridgeToken } from '../../api/aiBridge';

interface AiBridgeTokenSecretProps {
  created: AiBridgeToken;
  onClose: () => void;
}

export function AiBridgeTokenSecret({ created, onClose }: AiBridgeTokenSecretProps) {
  const [error, setError] = useState<string | null>(null);
  const command = mcpAddCommand(created.mcpUrl, created.token);
  const copy = async (text: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setError(null);
    } catch {
      setError('Could not copy. Select and copy the value manually.');
    }
  };

  return (
    <div className="settings-row ai-bridge-token">
      <p className="notice notice-caution">Copy the token now. TermForge keeps only its hash and cannot show it again.</p>
      <code className="mono ai-bridge-command" aria-label="Access token">{created.token}</code>
      <code className="mono ai-bridge-command" aria-label="Claude Code command">{command}</code>
      <div className="settings-table-actions">
        <button type="button" className="btn btn-sm" onClick={() => void copy(created.token)}>Copy token</button>
        <button type="button" className="btn btn-sm" onClick={() => void copy(command)}>Copy command</button>
        <button type="button" className="btn btn-sm" onClick={onClose}>Close token</button>
      </div>
      {error === null ? null : <p role="alert" className="form-error">{error}</p>}
    </div>
  );
}
