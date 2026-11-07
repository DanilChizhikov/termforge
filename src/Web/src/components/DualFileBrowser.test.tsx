import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { replaceConnections, resetConnections } from '../state/connections';
import { resetFilePaths } from '../state/filePaths';
import { selectServer } from '../state/selectedServer';
import type { SavedConnection } from '../api/connections';
import { browserTransfer, DualFileBrowser } from './DualFileBrowser';
import type { FilePaneProps } from './FilePane';

vi.mock('./FilePane', () => ({ FilePane: (props: FilePaneProps) => <div data-testid={`${props.title}-pane`}>
  <span>{props.label}</span><span>{props.path}</span>
  <button onClick={() => props.onPathChange(props.title === 'Left' ? '/left' : '/right')}>Navigate {props.title}</button>
  <button disabled={!props.transfer?.canSend}>Send {props.title}</button>
</div> }));
vi.mock('../api/files', async (original) => ({ ...await original<typeof import('../api/files')>(), getLocalRoots: () => Promise.resolve([]) }));

function server(id: string, readOnly = false): SavedConnection {
  return { id, name: id, readOnly } as SavedConnection;
}

beforeEach(() => {
  resetConnections(); resetFilePaths(); selectServer('server-a');
  replaceConnections([server('server-a'), server('server-b'), server('read-only', true)]);
});

describe('dual file browser', () => {
  it('keeps pane sources and paths independent from sidebar selection', async () => {
    render(<DualFileBrowser />);
    fireEvent.change(screen.getByLabelText('Left source'), { target: { value: 'server-b' } });
    fireEvent.click(screen.getByText('Navigate Left'));
    fireEvent.change(screen.getByLabelText('Right source'), { target: { value: 'server-b' } });
    fireEvent.click(screen.getByText('Navigate Right'));
    fireEvent.click(screen.getByText('Navigate Left'));
    selectServer('server-a');
    await waitFor(() => expect(screen.getByLabelText('Left source')).toHaveValue('server-b'));
    expect(screen.getByLabelText('Right source')).toHaveValue('server-b');
    expect(screen.getByTestId('Left-pane')).toHaveTextContent('/left');
    expect(screen.getByTestId('Right-pane')).toHaveTextContent('/right');
  });

  it('allows reading a read-only source and blocks writing into it', () => {
    render(<DualFileBrowser />);
    fireEvent.change(screen.getByLabelText('Left source'), { target: { value: 'read-only' } });
    fireEvent.click(screen.getByText('Navigate Left'));
    fireEvent.click(screen.getByText('Navigate Right'));
    expect(screen.getByText('Send Left')).toBeEnabled();
    expect(screen.getByText('Send Right')).toBeDisabled();
  });

  it('builds both remote directions and retains upload and download', () => {
    const a = { source: 'a', path: '/a' };
    const b = { source: 'b', path: '/b' };
    const local = { source: 'local', path: '/tmp' };
    expect(browserTransfer(a, b, ['/a/file'])).toMatchObject({ direction: 'remoteToRemote', connectionId: 'a', targetConnectionId: 'b', targetDirectory: '/b' });
    expect(browserTransfer(b, a, ['/b/file'])).toMatchObject({ connectionId: 'b', targetConnectionId: 'a' });
    expect(browserTransfer(local, a, ['/tmp/file'])).toMatchObject({ direction: 'upload', connectionId: 'a' });
    expect(browserTransfer(a, local, ['/a/file'])).toMatchObject({ direction: 'download', connectionId: 'a' });
    expect(browserTransfer(local, local, ['/tmp/file'])).toBeNull();
  });
});
