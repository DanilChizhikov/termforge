import { lazy, Suspense, type KeyboardEvent } from 'react';
import type { AiActionKind, AiApproval } from '../api/aiBridge';
import { languageForPath } from './editor/editorLanguage';
import { EmptyState } from './EmptyState';

const CodeDiff = lazy(() => import('./editor/CodeDiff'));

export interface AiApprovalDialogProps {
  approval: AiApproval;
  queued: number;
  error: string | null;
  busy: boolean;
  onDecide: (approve: boolean) => void;
}

const ACTION_TITLES: Record<AiActionKind, string> = {
  command: 'run a command',
  terminalInput: 'type into a terminal',
  containerAction: 'change a container',
  fileWrite: 'write a file',
};

export function AiApprovalDialog({ approval, queued, error, busy, onDecide }: AiApprovalDialogProps) {
  const titleId = `ai-approval-title-${approval.id}`;
  const descriptionId = `ai-approval-description-${approval.id}`;
  const fileChange = approval.fileChange;

  const denyOnEscape = (event: KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'Escape' && !busy) {
      onDecide(false);
    }
  };

  return (
    <div className="dialog-backdrop">
      <div
        role="alertdialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={descriptionId}
        className={fileChange === null ? 'dialog-card' : 'dialog-card ai-approval-dialog-diff'}
        onKeyDown={denyOnEscape}
      >
        <div className="dialog-header">
          <h2 id={titleId} className="dialog-title">
            Allow the AI agent to {ACTION_TITLES[approval.kind]} on {approval.serverName}?
          </h2>
          <p id={descriptionId} className="text-secondary">
            The request came through the AI bridge. Nothing runs until you approve it.
            {queued > 1 ? ` ${queued - 1} more waiting.` : ''}
          </p>
        </div>
        <div className="dialog-body">
          {fileChange === null ? (
            <pre className="mono ai-approval-summary">{approval.summary}</pre>
          ) : (
            <>
              <code className="mono">{fileChange.path}</code>
              <div className="ai-approval-diff">
                <Suspense fallback={<EmptyState message="Loading diff…" />}>
                  <CodeDiff
                    original={fileChange.before}
                    modified={fileChange.after}
                    language={languageForPath(fileChange.path).id}
                  />
                </Suspense>
              </div>
            </>
          )}
          {error === null ? null : <p className="notice notice-danger">{error}</p>}
        </div>
        <div className="dialog-footer">
          <button type="button" className="btn" disabled={busy} autoFocus onClick={() => onDecide(false)}>
            Deny
          </button>
          <button type="button" className="btn btn-primary" disabled={busy} onClick={() => onDecide(true)}>
            Approve
          </button>
        </div>
      </div>
    </div>
  );
}
