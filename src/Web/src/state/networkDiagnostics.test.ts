import { afterEach, expect, it } from 'vitest';
import { openNetworkDiagnostics, getNetworkDiagnostics, resetNetworkDiagnostics } from './networkDiagnostics';
import { activateWorkspaceTab, getActiveWorkspaceTab } from './workspaceTab';

afterEach(() => {
  resetNetworkDiagnostics();
  activateWorkspaceTab('workspace');
});

it('opens the dedicated network tab with a saved target without starting checks', () => {
  openNetworkDiagnostics('server-a');

  expect(getActiveWorkspaceTab()).toBe('network-diagnostics');
  expect(getNetworkDiagnostics().draft.targetConnectionId).toBe('server-a');
  expect(getNetworkDiagnostics().run).toBeNull();
  expect(getNetworkDiagnostics().busy).toBe(false);
});
