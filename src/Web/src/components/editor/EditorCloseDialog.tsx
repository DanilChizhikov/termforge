import { cancelEditorFileClose, discardAndCloseEditorFile, usePendingEditorClose } from '../../state/editorClose';
import { useEditorFiles } from '../../state/editorFiles';

export function EditorCloseDialog() {
  const pendingKey = usePendingEditorClose();
  const { files } = useEditorFiles();
  const closing = files.find((file) => file.key === pendingKey) ?? null;

  if (closing === null) {
    return null;
  }

  return (
    <div role="alertdialog" aria-label="Discard unsaved changes" className="file-action file-action-danger editor-close-dialog">
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
}
