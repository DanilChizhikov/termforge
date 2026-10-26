import { useEffect, useState } from 'react';
import {
  activateEditorFile,
  closeEditorFile,
  useEditorFiles,
  type EditorFile,
} from '../../state/editorFiles';
import { EmptyState } from '../EmptyState';
import { CloseIcon, FileIcon } from '../icons/Icons';
import { EditorDocumentView } from './EditorDocumentView';

function editorTabClassName(active: boolean): string {
  return active ? 'terminal-tab terminal-tab-active' : 'terminal-tab';
}

export function EditorWorkspace() {
  const { files, activeKey } = useEditorFiles();
  const [closing, setClosing] = useState<EditorFile | null>(null);
  const hasUnsaved = files.some((file) => file.dirty);

  useEffect(() => {
    if (!hasUnsaved) {
      return undefined;
    }
    const warn = (event: BeforeUnloadEvent) => {
      event.preventDefault();
    };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, [hasUnsaved]);

  function requestClose(file: EditorFile): void {
    if (file.dirty) {
      setClosing(file);
      return;
    }
    closeEditorFile(file.key);
  }

  if (files.length === 0) {
    return <EmptyState message="No files open. Double-click a file in the remote pane to edit it." />;
  }

  return (
    <div className="editor-workspace">
      <div className="terminal-bar">
        <div role="tablist" aria-label="Open files" className="terminal-tab-list">
          {files.map((file) => (
            <span key={file.key} className={editorTabClassName(file.key === activeKey)}>
              <button
                type="button"
                role="tab"
                id={`tab-${file.key}`}
                aria-selected={file.key === activeKey}
                aria-controls={`panel-${file.key}`}
                className="terminal-tab-button"
                title={file.path}
                onClick={() => activateEditorFile(file.key)}
              >
                <FileIcon size={13} />
                <span className="mono">{file.name}</span>
                {file.dirty ? (
                  <>
                    <span className="editor-dirty-dot" aria-hidden="true" />
                    <span className="visually-hidden">(unsaved)</span>
                  </>
                ) : null}
              </button>
              <button
                type="button"
                className="icon-btn icon-btn-sm"
                aria-label={`Close ${file.name}`}
                onClick={() => requestClose(file)}
              >
                <CloseIcon size={12} />
              </button>
            </span>
          ))}
        </div>
      </div>
      {closing === null ? null : (
        <div role="alertdialog" aria-label="Discard unsaved changes" className="file-action file-action-danger">
          <p>
            Close <span className="mono">{closing.name}</span> and discard your unsaved changes? The file on the server
            stays as it is.
          </p>
          <div className="file-action-buttons">
            <button type="button" className="btn btn-sm btn-safe" onClick={() => setClosing(null)}>
              Keep editing
            </button>
            <button
              type="button"
              className="btn btn-sm btn-destructive"
              onClick={() => {
                closeEditorFile(closing.key);
                setClosing(null);
              }}
            >
              Discard and close
            </button>
          </div>
        </div>
      )}
      {files.map((file) => (
        <div
          key={file.key}
          role="tabpanel"
          id={`panel-${file.key}`}
          aria-labelledby={`tab-${file.key}`}
          hidden={file.key !== activeKey}
          className="editor-panel"
        >
          <EditorDocumentView file={file} />
        </div>
      ))}
    </div>
  );
}
