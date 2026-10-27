import { useConnectionsSnapshot } from '../../state/connections';
import { requestEditorFileClose } from '../../state/editorClose';
import { activateEditorFile, useEditorFiles } from '../../state/editorFiles';
import { activateWorkspaceTab } from '../../state/workspaceTab';
import { CloseIcon, FileIcon } from '../icons/Icons';

export interface EditorFileTabsProps {
  editorActive: boolean;
}

export function EditorFileTabs({ editorActive }: EditorFileTabsProps) {
  const { files, activeKey } = useEditorFiles();
  const connections = useConnectionsSnapshot();

  return (
    <>
      {files.map((file) => {
        const selected = editorActive && file.key === activeKey;
        const serverName = connections.find((connection) => connection.id === file.connectionId)?.name ?? null;
        return (
          <span key={file.key} className={selected ? 'workspace-tab-item workspace-tab-item-active' : 'workspace-tab-item'}>
            <button
              type="button"
              role="tab"
              id={`tab-${file.key}`}
              aria-selected={selected}
              aria-controls={`panel-${file.key}`}
              className="workspace-tab"
              title={file.path}
              onClick={() => {
                activateEditorFile(file.key);
                activateWorkspaceTab('editor');
              }}
            >
              {selected ? <FileIcon /> : null}
              {file.name}
              {serverName === null ? null : (
                <>
                  {' '}
                  <span className="workspace-tab-context">· {serverName}</span>
                </>
              )}
              {file.dirty ? (
                <>
                  <span className="workspace-tab-dirty" aria-hidden="true" />
                  {' '}
                  <span className="visually-hidden">(unsaved)</span>
                </>
              ) : null}
            </button>
            <button
              type="button"
              className="icon-btn icon-btn-sm workspace-tab-close"
              aria-label={`Close ${file.name}`}
              onClick={() => requestEditorFileClose(file.key)}
            >
              <CloseIcon size={12} />
            </button>
          </span>
        );
      })}
    </>
  );
}
