import { createPortal } from 'react-dom';
import { cancelEditorFileClose, discardAndCloseEditorFile, usePendingEditorClose } from '../../state/editorClose';
import { useEditorFiles } from '../../state/editorFiles';
import { usePanelPlacement } from '../workspace/WorkspacePanelProvider';

export function EditorCloseDialog() {
  const pendingKey = usePendingEditorClose();
  const floating = usePanelPlacement(pendingKey ?? '')?.mode === 'floating';
  const { files } = useEditorFiles();
  const closing = files.find((file) => file.key === pendingKey) ?? null;

  if (closing === null) {
    return null;
  }

  const prompt = (
    <div role="alertdialog" aria-label="Discard unsaved changes" aria-modal={floating || undefined} className="file-action file-action-danger editor-close-dialog">
      <p>
        Close <span className="mono">{closing.name}</span> and discard your unsaved changes? The file on the server stays
        as it is.
      </p>
      <div className="file-action-buttons">
        <button type="button" className="btn btn-sm btn-safe" autoFocus onClick={cancelEditorFileClose}>
          Keep editing
        </button>
        <button type="button" className="btn btn-sm btn-destructive" onClick={() => discardAndCloseEditorFile(closing.key)}>
          Discard and close
        </button>
      </div>
    </div>
  );
  return floating ? createPortal(<div className="dialog-backdrop">{prompt}</div>, document.body) : prompt;
}
