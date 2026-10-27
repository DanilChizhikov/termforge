import type { editor } from 'monaco-editor/editor/editor.api';
import { readThemeToken, readThemeTokenHex } from '../themeTokens';
import { TERMINAL_FONT_FAMILY, TERMINAL_FONT_SIZE } from '../terminalTheme';

export const EDITOR_THEME_NAME = 'termforge-dark';

export function createEditorTheme(): editor.IStandaloneThemeData {
  return {
    base: 'vs-dark',
    inherit: true,
    rules: [
      { token: 'comment', foreground: readThemeTokenHex('--text-muted'), fontStyle: 'italic' },
      { token: 'keyword', foreground: readThemeTokenHex('--status-info-text') },
      { token: 'string', foreground: readThemeTokenHex('--code-string') },
      { token: 'number', foreground: readThemeTokenHex('--status-info-text') },
      { token: 'type', foreground: readThemeTokenHex('--status-info-text') },
      { token: 'variable', foreground: readThemeTokenHex('--text-soft') },
    ],
    colors: {
      'editor.background': readThemeToken('--surface-terminal'),
      'editor.foreground': readThemeToken('--text-primary'),
      'editorCursor.foreground': readThemeToken('--accent'),
      'editor.lineHighlightBackground': readThemeToken('--surface-card'),
      'editor.selectionBackground': readThemeToken('--code-selection'),
      'editorLineNumber.foreground': readThemeToken('--code-line-number'),
      'editorLineNumber.activeForeground': readThemeToken('--text-secondary'),
      'editorGutter.background': readThemeToken('--surface-terminal'),
      'diffEditor.insertedTextBackground': readThemeToken('--diff-local-text'),
      'diffEditor.removedTextBackground': readThemeToken('--diff-server-text'),
      'diffEditor.insertedLineBackground': readThemeToken('--diff-local-line'),
      'diffEditor.removedLineBackground': readThemeToken('--diff-server-line'),
    },
  };
}

export const EDITOR_OPTIONS: editor.IStandaloneEditorConstructionOptions = {
  fontFamily: TERMINAL_FONT_FAMILY,
  fontSize: TERMINAL_FONT_SIZE,
  lineHeight: 21,
  minimap: { enabled: false },
  scrollBeyondLastLine: false,
  automaticLayout: true,
  renderWhitespace: 'selection',
  tabSize: 2,
};
