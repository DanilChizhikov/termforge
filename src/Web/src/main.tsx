import '@fontsource-variable/geist';
import '@fontsource-variable/geist-mono';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './App';
import { loadTerminalFont } from './components/terminalTheme';
import { restoreWorkspace, startWorkspaceAutosave } from './state/workspacePersistence';
import './styles/tokens.css';
import './styles/base.css';
import './styles/components.css';
import './styles/layout.css';
import './styles/docker.css';
import './styles/settings.css';
import './styles/recording.css';

const container = document.getElementById('root');
if (container === null) {
  throw new Error('Root element not found');
}

const root = createRoot(container);

function renderApp(): void {
  root.render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
}

async function start(): Promise<void> {
  const [, restored] = await Promise.all([loadTerminalFont().catch(() => undefined), restoreWorkspace()]);
  renderApp();
  if (restored) {
    startWorkspaceAutosave();
  }
}

void start();
