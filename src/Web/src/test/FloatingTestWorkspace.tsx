import { act, render } from '@testing-library/react';
import { useLayoutEffect, type ReactNode } from 'react';
import { WorkspaceCanvas } from '../components/workspace/WorkspaceCanvas';
import { useWorkspacePanels, WorkspacePanelProvider } from '../components/workspace/WorkspacePanelProvider';

type WorkspacePanels = NonNullable<ReturnType<typeof useWorkspacePanels>>;

export const INITIAL_FLOATING_RECT = { x: 16, y: 24, width: 480, height: 320 };

function ReadWorkspace({ read }: { read: (value: WorkspacePanels) => void }) {
  const panels = useWorkspacePanels();
  useLayoutEffect(() => { if (panels !== null) read(panels); }, [panels, read]);
  return null;
}

export function renderInWorkspace(children: ReactNode) {
  let panels: WorkspacePanels;
  const read = (value: WorkspacePanels) => { panels = value; };
  const view = render(
    <WorkspacePanelProvider>
      <WorkspaceCanvas>
        <ReadWorkspace read={read} />
        {children}
      </WorkspaceCanvas>
    </WorkspacePanelProvider>,
  );
  return {
    ...view,
    panels: () => panels,
    detach: (id: string) => act(() => panels.detachPanel(id, INITIAL_FLOATING_RECT)),
  };
}
