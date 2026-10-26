import type { SshConfigImportResult } from '../api/connections';

export interface ImportReportProps {
  result: SshConfigImportResult;
  onDismiss: () => void;
}

export function ImportReport({ result, onDismiss }: ImportReportProps) {
  return (
    <div className="import-report" role="status">
      <p>
        Imported from {result.configPath}: {result.created} added, {result.updated} updated.
      </p>
      {result.issues.length === 0 ? null : (
        <>
          <p>Not applied ({result.issues.length}):</p>
          <ul>
            {result.issues.map((issue) => (
              <li key={`${issue.filePath}:${issue.line}:${issue.reason}`}>
                {issue.directive} ({issue.filePath}
                {issue.line > 0 ? `:${issue.line}` : ''}): {issue.reason}
                {issue.affectedAliases.length > 0 ? ` Hosts: ${issue.affectedAliases.join(', ')}.` : ''}
              </li>
            ))}
          </ul>
        </>
      )}
      <button type="button" className="btn btn-xs" onClick={onDismiss}>
        Dismiss
      </button>
    </div>
  );
}
