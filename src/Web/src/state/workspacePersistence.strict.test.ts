import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiClient } from '../api/client';
import { flushWorkspaceState, flushWorkspaceStateStrict, startWorkspaceAutosave } from './workspacePersistence';

let stop: (() => void) | undefined;
afterEach(() => { stop?.(); stop = undefined; });

describe('strict workspace persistence', () => {
  it('blocks restart when workspace persistence has not started', async () => {
    await expect(flushWorkspaceStateStrict()).rejects.toThrow('not ready');
    await expect(flushWorkspaceState()).resolves.toBeUndefined();
  });

  it('reports a failed write and retries the same snapshot successfully', async () => {
    const fetchImpl = vi.fn().mockResolvedValueOnce(new Response('unavailable', { status: 503 }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    stop = startWorkspaceAutosave({ client: new ApiClient({ fetchImpl }) });

    await expect(flushWorkspaceStateStrict()).rejects.toThrow();
    await expect(flushWorkspaceStateStrict()).resolves.toBeUndefined();
    expect(fetchImpl).toHaveBeenCalledTimes(2);
  });

  it('waits for persistence to acknowledge the current snapshot', async () => {
    let finish: ((response: Response) => void) | undefined;
    const fetchImpl = vi.fn(() => new Promise<Response>((resolve) => { finish = resolve; }));
    stop = startWorkspaceAutosave({ client: new ApiClient({ fetchImpl }) });
    let completed = false;
    const saving = flushWorkspaceStateStrict().then(() => { completed = true; });
    await vi.waitFor(() => expect(fetchImpl).toHaveBeenCalled());
    expect(completed).toBe(false);
    finish!(new Response(null, { status: 204 }));
    await saving;
    expect(completed).toBe(true);
  });
});
