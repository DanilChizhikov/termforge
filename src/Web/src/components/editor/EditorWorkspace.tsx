import { useEffect } from 'react';
import { useEditorFiles } from '../../state/editorFiles';
import { EmptyState } from '../EmptyState';
import { EditorDocumentView } from './EditorDocumentView';

export function EditorWorkspace() {
  const { files, activeKey } = useEditorFiles();
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

  if (files.length === 0) {
    return <EmptyState message="No files open. Double-click a file in the remote pane to edit it." />;
  }

  return (
    <div className="editor-workspace">
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
