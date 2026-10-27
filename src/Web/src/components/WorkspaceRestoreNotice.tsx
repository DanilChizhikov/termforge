import { dismissWorkspaceRestoreWarning, useWorkspaceRestoreWarning } from '../state/workspacePersistence';

export function WorkspaceRestoreNotice() {
  const warning = useWorkspaceRestoreWarning();
  if (warning === null) {
    return null;
  }
  return (
    <div role="alert" className="notice notice-caution workspace-restore-notice">
      <span>{warning.message}</span>
      <button type="button" className="btn btn-xs" onClick={dismissWorkspaceRestoreWarning}>
        Dismiss
      </button>
    </div>
  );
}
