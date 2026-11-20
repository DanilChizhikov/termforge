import { render } from '@testing-library/react';
import { StrictMode } from 'react';
import { afterEach, expect, it, vi } from 'vitest';
import { App } from './App';

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

it('removes the window capture handler on StrictMode effect cleanup and on unmount', () => {
  const unauthorized = () => Promise.resolve(new Response(null, { status: 401 }));
  vi.stubGlobal('fetch', vi.fn(unauthorized));
  const addListener = vi.spyOn(window, 'addEventListener');
  const removeListener = vi.spyOn(window, 'removeEventListener');

  const { unmount } = render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
  const registrations = addListener.mock.calls.filter(([type, , capture]) => type === 'keydown' && capture === true);

  expect(registrations).toHaveLength(2);
  expect(removeListener).toHaveBeenCalledWith('keydown', registrations[0]![1], true);
  expect(removeListener).not.toHaveBeenCalledWith('keydown', registrations[1]![1], true);

  unmount();

  expect(removeListener).toHaveBeenCalledWith('keydown', registrations[1]![1], true);
});
