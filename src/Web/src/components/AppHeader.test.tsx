import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { showAppView } from '../state/appView';
import { AppHeader } from './AppHeader';

beforeEach(() => {
  const unauthorized = () => Promise.resolve(new Response(null, { status: 401 }));
  vi.stubGlobal('fetch', vi.fn(unauthorized));
  act(() => showAppView('workspace'));
});

describe('AppHeader brand', () => {
  it('returns from settings to the workspace when the brand is clicked', async () => {
    act(() => showAppView('settings'));
    const user = userEvent.setup();
    render(<AppHeader />);

    await user.click(screen.getByRole('button', { name: 'TermForge' }));

    expect(screen.getByRole('button', { name: 'Settings' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Back to workspace/ })).not.toBeInTheDocument();
  });

  it('is not a button in the workspace', () => {
    render(<AppHeader />);

    expect(screen.getByText('TermForge')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'TermForge' })).not.toBeInTheDocument();
  });
});
