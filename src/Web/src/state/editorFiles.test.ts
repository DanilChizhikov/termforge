import { beforeEach, describe, expect, it } from 'vitest';
import { closeEditorFile, editorFileKey, openEditorFile, resetEditorFiles, setEditorFileDirty, useEditorFiles } from './editorFiles';
import { activateWorkspaceTab, useActiveWorkspaceTab } from './workspaceTab';
import { act, renderHook } from '@testing-library/react';

beforeEach(() => {
  resetEditorFiles();
  activateWorkspaceTab('workspace');
});

describe('editorFiles', () => {
  it('opens a file once, activates it and switches to the editor tab', () => {
    const files = renderHook(() => useEditorFiles());
    const tab = renderHook(() => useActiveWorkspaceTab());

    act(() => {
      openEditorFile('s1', '/etc/a.conf');
      openEditorFile('s1', '/etc/b.conf');
      openEditorFile('s1', '/etc/a.conf');
    });

    expect(files.result.current.files.map((file) => file.name)).toEqual(['a.conf', 'b.conf']);
    expect(files.result.current.activeKey).toBe(editorFileKey('s1', '/etc/a.conf'));
    expect(tab.result.current).toBe('editor');
  });

  it('activates a neighbour when the active file is closed', () => {
    const files = renderHook(() => useEditorFiles());
    act(() => {
      openEditorFile('s1', '/a');
      openEditorFile('s1', '/b');
      setEditorFileDirty(editorFileKey('s1', '/b'), true);
      closeEditorFile(editorFileKey('s1', '/b'));
    });

    expect(files.result.current.files).toHaveLength(1);
    expect(files.result.current.activeKey).toBe(editorFileKey('s1', '/a'));
  });
});
