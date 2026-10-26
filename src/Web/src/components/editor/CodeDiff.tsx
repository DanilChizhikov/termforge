import { DiffEditor } from '@monaco-editor/react';
import './monacoSetup';
import { EDITOR_OPTIONS, EDITOR_THEME_NAME } from './editorTheme';

export interface CodeDiffProps {
  original: string;
  modified: string;
  language: string;
}

export default function CodeDiff({ original, modified, language }: CodeDiffProps) {
  return (
    <DiffEditor
      original={original}
      modified={modified}
      language={language}
      theme={EDITOR_THEME_NAME}
      options={{ ...EDITOR_OPTIONS, readOnly: true, originalEditable: false, renderSideBySide: true }}
    />
  );
}
