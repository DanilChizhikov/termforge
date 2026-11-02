import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';
import { resetWorkspaceLayout } from '../state/workspaceLayout';
import { clearOriginProof } from '../api/originProof';

class ResizeObserverStub {
  observe(): void {}
  unobserve(): void {}
  disconnect(): void {}
}

globalThis.ResizeObserver = ResizeObserverStub;

afterEach(async () => {
  cleanup();
  clearOriginProof();
  resetWorkspaceLayout();
  const { resetConnections } = await import('../state/connections');
  resetConnections();
  const { resetUpdates } = await import('../state/updates');
  resetUpdates();
});
