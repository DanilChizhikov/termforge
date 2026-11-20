import { useEffect } from 'react';
import { requestEditorFileClose } from '../../state/editorClose';
import { useEditorFiles } from '../../state/editorFiles';
import { EmptyState } from '../EmptyState';
import { FileIcon } from '../icons/Icons';
import { MovablePanel } from '../workspace/MovablePanel';
import { useWorkspacePanels } from '../workspace/WorkspacePanelProvider';
import { EditorDocumentView } from './EditorDocumentView';

export function EditorWorkspace() {
  const panels = useWorkspacePanels();
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
        <MovablePanel key={file.key} id={file.key} title={file.name} Icon={FileIcon} onClose={() => requestEditorFileClose(file.key)}>
          <div
            role="tabpanel"
            id={`panel-${file.key}`}
            aria-labelledby={`tab-${file.key}`}
            hidden={file.key !== activeKey && panels?.placements.get(file.key)?.mode !== 'floating'}
            className="editor-panel"
          >
            <EditorDocumentView file={file} />
          </div>
        </MovablePanel>
      ))}
    </div>
  );
}
