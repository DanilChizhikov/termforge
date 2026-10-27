import { loader } from '@monaco-editor/react';
import * as monaco from 'monaco-editor/editor/editor.api';
import 'monaco-editor/languages/definitions/dockerfile/register';
import 'monaco-editor/languages/definitions/ini/register';
import 'monaco-editor/languages/definitions/markdown/register';
import 'monaco-editor/languages/definitions/python/register';
import 'monaco-editor/languages/definitions/shell/register';
import 'monaco-editor/languages/definitions/xml/register';
import 'monaco-editor/languages/definitions/yaml/register';
import 'monaco-editor/languages/features/json/register';
import EditorWorker from 'monaco-editor/editor/editor.worker?worker';
import JsonWorker from 'monaco-editor/languages/features/json/json.worker?worker';
import { createEditorTheme, EDITOR_THEME_NAME } from './editorTheme';

globalThis.MonacoEnvironment = {
  getWorker(_workerId: string, label: string) {
    return label === 'json' ? new JsonWorker() : new EditorWorker();
  },
};

const editorTheme = createEditorTheme();
monaco.editor.defineTheme(EDITOR_THEME_NAME, editorTheme);
loader.config({ monaco });
