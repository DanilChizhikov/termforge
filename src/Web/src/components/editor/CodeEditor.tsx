import Editor from '@monaco-editor/react';
import { useEffect, useRef } from 'react';
import './monacoSetup';
import { EDITOR_OPTIONS, EDITOR_THEME_NAME } from './editorTheme';

export interface CodeEditorProps {
  value: string;
  language: string;
  label: string;
  onChange: (text: string) => void;
  onCursorChange: (line: number, column: number) => void;
}

export default function CodeEditor({ value, language, label, onChange, onCursorChange }: CodeEditorProps) {
  const cursorRef = useRef(onCursorChange);

  useEffect(() => {
    cursorRef.current = onCursorChange;
  }, [onCursorChange]);

  return (
    <Editor
      value={value}
      language={language}
      theme={EDITOR_THEME_NAME}
      options={{ ...EDITOR_OPTIONS, ariaLabel: label }}
      onChange={(text) => onChange(text ?? '')}
      onMount={(editor) => {
        editor.onDidChangeCursorPosition((event) => {
          cursorRef.current(event.position.lineNumber, event.position.column);
        });
      }}
    />
  );
}
