import { render } from '@testing-library/react';
import { StrictMode } from 'react';
import { afterEach, expect, it, vi } from 'vitest';
import { App } from './App';
import * as globalShortcuts from './keyboard/globalShortcuts';

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
});

it('disposes global shortcuts before StrictMode reinstalls them and on unmount', () => {
  const unauthorized = () => Promise.resolve(new Response(null, { status: 401 }));
  vi.stubGlobal('fetch', vi.fn(unauthorized));
  const disposeFirst = vi.fn();
  const disposeSecond = vi.fn();
  const install = vi.spyOn(globalShortcuts, 'installGlobalShortcuts')
    .mockReturnValueOnce(disposeFirst)
    .mockReturnValueOnce(disposeSecond);

  const { unmount } = render(
    <StrictMode>
      <App />
    </StrictMode>,
  );
  expect(install).toHaveBeenCalledTimes(2);
  expect(disposeFirst).toHaveBeenCalledOnce();
  expect(disposeFirst.mock.invocationCallOrder[0]).toBeLessThan(install.mock.invocationCallOrder[1]!);
  expect(disposeSecond).not.toHaveBeenCalled();

  unmount();

  expect(disposeFirst).toHaveBeenCalledOnce();
  expect(disposeSecond).toHaveBeenCalledOnce();
});
