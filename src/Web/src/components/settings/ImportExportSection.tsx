import { useState } from 'react';
import { ExportSettingsDialog } from './ExportSettingsDialog';
import { ImportSettingsDialog } from './ImportSettingsDialog';

type OpenDialog = 'export' | 'import' | null;

export function ImportExportSection() {
  const [openDialog, setOpenDialog] = useState<OpenDialog>(null);
  const [exportedFiles, setExportedFiles] = useState<string[]>([]);

  const finishExport = (fileNames: string[]) => {
    setExportedFiles(fileNames);
    setOpenDialog(null);
  };

  return (
    <section id="settings-import-export" className="settings-section" aria-labelledby="settings-import-export-title">
      <div className="settings-section-header">
        <div className="settings-row-text">
          <h2 id="settings-import-export-title" className="settings-section-title">
            Import &amp; export
          </h2>
          <span className="settings-row-hint">
            Move TermForge to another computer, or keep a backup. Passwords leave this computer only in a separate
            encrypted archive.
          </span>
        </div>
      </div>
      <div className="settings-card">
        <div className="settings-row">
          <span className="settings-row-text">
            <span className="settings-row-title">Export settings</span>
            <span className="settings-row-hint">Server connections, preferences, workspace layout and pinned commands as a JSON file.</span>
          </span>
          <button type="button" className="btn btn-sm" onClick={() => setOpenDialog('export')}>
            Export…
          </button>
        </div>
        <div className="settings-row">
          <span className="settings-row-text">
            <span className="settings-row-title">Import settings</span>
            <span className="settings-row-hint">Preview what changes and resolve conflicts before anything is applied.</span>
          </span>
          <button type="button" className="btn btn-sm" onClick={() => setOpenDialog('import')}>
            Import…
          </button>
        </div>
        {exportedFiles.length > 0 ? (
          <p role="status" className="settings-row settings-row-hint">
            Saved{' '}
            {exportedFiles.map((fileName, index) => (
              <span key={fileName}>
                {index > 0 ? ' and ' : ''}
                <code className="mono">{fileName}</code>
              </span>
            ))}
            .
          </p>
        ) : null}
      </div>
      {openDialog === 'export' ? <ExportSettingsDialog onClose={() => setOpenDialog(null)} onExported={finishExport} /> : null}
      {openDialog === 'import' ? <ImportSettingsDialog onClose={() => setOpenDialog(null)} /> : null}
    </section>
  );
}
