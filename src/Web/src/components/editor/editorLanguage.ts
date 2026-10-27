export interface EditorLanguage {
  id: string;
  label: string;
}

const PLAIN_TEXT: EditorLanguage = { id: 'plaintext', label: 'Plain text' };
const SHELL: EditorLanguage = { id: 'shell', label: 'Shell' };
const YAML: EditorLanguage = { id: 'yaml', label: 'YAML' };
const JSON_LANGUAGE: EditorLanguage = { id: 'json', label: 'JSON' };
const INI: EditorLanguage = { id: 'ini', label: 'INI / config' };
const DOCKERFILE: EditorLanguage = { id: 'dockerfile', label: 'Dockerfile' };
const XML: EditorLanguage = { id: 'xml', label: 'XML' };
const MARKDOWN: EditorLanguage = { id: 'markdown', label: 'Markdown' };
const PYTHON: EditorLanguage = { id: 'python', label: 'Python' };

const BY_EXTENSION: Readonly<Record<string, EditorLanguage>> = {
  sh: SHELL,
  bash: SHELL,
  zsh: SHELL,
  ksh: SHELL,
  yml: YAML,
  yaml: YAML,
  json: JSON_LANGUAGE,
  ini: INI,
  conf: INI,
  cfg: INI,
  cnf: INI,
  env: INI,
  properties: INI,
  service: INI,
  timer: INI,
  socket: INI,
  xml: XML,
  md: MARKDOWN,
  py: PYTHON,
};

const BY_NAME: Readonly<Record<string, EditorLanguage>> = {
  dockerfile: DOCKERFILE,
  containerfile: DOCKERFILE,
  '.bashrc': SHELL,
  '.bash_profile': SHELL,
  '.profile': SHELL,
  '.zshrc': SHELL,
  '.env': INI,
  '.gitconfig': INI,
  '.editorconfig': INI,
};

export function languageForPath(path: string): EditorLanguage {
  const name = path.slice(path.lastIndexOf('/') + 1).toLowerCase();
  const byName = BY_NAME[name];
  if (byName !== undefined) {
    return byName;
  }
  if (name.startsWith('dockerfile.')) {
    return DOCKERFILE;
  }
  const dot = name.lastIndexOf('.');
  if (dot <= 0) {
    return PLAIN_TEXT;
  }
  return BY_EXTENSION[name.slice(dot + 1)] ?? PLAIN_TEXT;
}
