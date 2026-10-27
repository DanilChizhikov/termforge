import { EditorCloseDialog } from '../components/editor/EditorCloseDialog';
import { EditorFileTabs } from '../components/editor/EditorFileTabs';
import { EditorWorkspace } from '../components/editor/EditorWorkspace';

export function EditorHarness() {
  return (
    <>
      <EditorFileTabs editorActive />
      <EditorCloseDialog />
      <EditorWorkspace />
    </>
  );
}
