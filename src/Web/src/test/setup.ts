import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';
import { resetWorkspaceLayout } from '../state/workspaceLayout';

class ResizeObserverStub {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}

globalThis.ResizeObserver = ResizeObserverStub;

afterEach(async () => {
  cleanup();
  resetWorkspaceLayout();
  const { resetConnections } = await import('../state/connections');
  resetConnections();
});
