import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiClient } from './client';
import { getOriginProof, saveOriginProof } from './originProof';
import { signIn, signOut } from './session';

afterEach(() => vi.unstubAllGlobals());

describe('session origin proof', () => {
  it('persists the proof from a successful password login', async () => {
    const response = new Response(JSON.stringify({ csrfToken: 'password-proof' }), { status: 200 });
    vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockResolvedValue(response));

    const result = await signIn('secret');

    expect(result).toEqual({ kind: 'signed-in' });
    expect(getOriginProof()).toBe('password-proof');
  });

  it('does not issue a proof after a rejected password login', async () => {
    const response = new Response(null, { status: 401 });
    vi.stubGlobal('fetch', vi.fn<typeof fetch>().mockResolvedValue(response));

    const result = await signIn('wrong');

    expect(result).toEqual({ kind: 'wrong-password' });
    expect(getOriginProof()).toBeNull();
  });

  it('removes the proof after a successful logout', async () => {
    saveOriginProof('logout-proof');
    const fetchImpl = vi.fn<typeof fetch>().mockResolvedValue(new Response(null, { status: 204 }));
    const client = new ApiClient({ fetchImpl });

    await signOut(client);

    expect(getOriginProof()).toBeNull();
    const headers = fetchImpl.mock.calls[0]?.[1]?.headers as Record<string, string>;
    expect(headers['X-CSRF-Token']).toBe('logout-proof');
  });
});
