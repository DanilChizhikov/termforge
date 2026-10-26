import type { editor } from 'monaco-editor/editor/editor.api';
import { TERMINAL_FONT_FAMILY, TERMINAL_FONT_SIZE } from '../terminalTheme';

export const EDITOR_THEME_NAME = 'termforge-dark';

export const EDITOR_THEME: editor.IStandaloneThemeData = {
  base: 'vs-dark',
  inherit: true,
  rules: [
    { token: 'comment', foreground: '858b94', fontStyle: 'italic' },
    { token: 'keyword', foreground: 'ffb27f' },
    { token: 'string', foreground: '7fd8ae' },
    { token: 'number', foreground: '9cc3ff' },
    { token: 'type', foreground: '9cc3ff' },
    { token: 'variable', foreground: 'c9cdd3' },
  ],
  colors: {
    'editor.background': '#08090b',
    'editor.foreground': '#d5d8dd',
    'editorCursor.foreground': '#ff8a3d',
    'editor.lineHighlightBackground': '#121417',
    'editor.selectionBackground': '#ff8a3d47',
    'editorLineNumber.foreground': '#6b717a',
    'editorLineNumber.activeForeground': '#a9aeb6',
    'editorGutter.background': '#08090b',
    'diffEditor.insertedTextBackground': '#46c08a2e',
    'diffEditor.removedTextBackground': '#ff6b5e2e',
    'diffEditor.insertedLineBackground': '#46c08a14',
    'diffEditor.removedLineBackground': '#ff6b5e14',
  },
};

export const EDITOR_OPTIONS: editor.IStandaloneEditorConstructionOptions = {
  fontFamily: TERMINAL_FONT_FAMILY,
  fontSize: TERMINAL_FONT_SIZE,
  minimap: { enabled: false },
  scrollBeyondLastLine: false,
  automaticLayout: true,
  renderWhitespace: 'selection',
  tabSize: 2,
};
