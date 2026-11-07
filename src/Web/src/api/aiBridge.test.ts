import { describe, expect, it, vi } from 'vitest';
import { ApiClient } from './client';
import { createAiBridgeToken, listAiBridgeTokens, revokeAiBridgeToken } from './aiBridge';

describe('AI bridge tokens API', () => {
  it('uses the named token routes and sends the name', async () => {
    const fetchImpl = vi.fn().mockImplementation(() => Promise.resolve(new Response('{}', { status: 200 })));
    const client = new ApiClient({ fetchImpl });
    await createAiBridgeToken('Agent', client);
    expect(fetchImpl).toHaveBeenLastCalledWith('/api/ai-bridge/tokens', expect.objectContaining({ method: 'POST', body: '{"name":"Agent"}' }));
    await listAiBridgeTokens(client);
    expect(fetchImpl).toHaveBeenLastCalledWith('/api/ai-bridge/tokens', expect.objectContaining({ method: 'GET' }));
    await revokeAiBridgeToken('../other', client);
    expect(fetchImpl).toHaveBeenLastCalledWith('/api/ai-bridge/tokens/..%2Fother', expect.objectContaining({ method: 'DELETE' }));
  });
});
