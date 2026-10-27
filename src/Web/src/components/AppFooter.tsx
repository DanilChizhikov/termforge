import type { AuthMethod } from '../api/connections';
import { useConnection } from '../state/connections';
import { useSelectedServerId } from '../state/selectedServer';
import { useWorkspaceSavedAt } from '../state/workspaceSaved';
import { formatClock } from './timeFormat';

const AUTH_LABELS: Record<AuthMethod, string> = {
  Agent: 'SSH agent',
  KeyFile: 'Key file',
  Password: 'Password',
};

export function AppFooter() {
  const connection = useConnection(useSelectedServerId());
  const savedAt = useWorkspaceSavedAt();

  return (
    <footer className="app-footer">
      {connection === null ? null : (
        <>
          <span>Auth: {AUTH_LABELS[connection.authMethod]}</span>
          <span>Transport: SSH (PTY) + SFTP</span>
        </>
      )}
      <span className="app-footer-spacer" />
      {savedAt === null ? null : <span>Workspace saved {formatClock(savedAt)}</span>}
    </footer>
  );
}
