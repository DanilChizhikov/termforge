import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { App } from './App';

beforeEach(() => {
  const unauthorized = () => Promise.resolve(new Response(null, { status: 401 }));
  vi.stubGlobal('fetch', vi.fn(unauthorized));
});

describe('layout', () => {
  it('renders sidebar, terminals and two file panes', () => {
    render(<App />);

    expect(screen.getByRole('region', { name: 'Servers' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Terminals' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Files' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Local' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Remote' })).toBeInTheDocument();
  });

  it('renders resize separators between regions', () => {
    render(<App />);

    expect(screen.getAllByRole('separator')).toHaveLength(3);
  });

  it('shows empty states without fake data', () => {
    render(<App />);

    expect(screen.getByText('No servers yet.')).toBeInTheDocument();
    expect(screen.getByText(/No terminal sessions/)).toBeInTheDocument();
  });

  it('shows a banner when the session is missing', async () => {
    render(<App />);

    expect(await screen.findByRole('alert')).toHaveTextContent(/Restart TermForge/);
  });
});

describe('collapsible regions', () => {
  it.each(['Servers', 'Terminals', 'Files', 'Local', 'Remote'])('collapses and expands %s', async (title) => {
    const user = userEvent.setup();
    render(<App />);
    const region = screen.getByRole('region', { name: title });

    await user.click(within(region).getAllByRole('button', { name: `Collapse ${title}` })[0]!);
    const expandButton = within(screen.getByRole('region', { name: title })).getAllByRole('button', {
      name: `Expand ${title}`,
    })[0]!;
    expect(expandButton).toHaveAttribute('aria-expanded', 'false');

    await user.click(expandButton);
    expect(
      within(screen.getByRole('region', { name: title })).getAllByRole('button', { name: `Collapse ${title}` })[0],
    ).toHaveAttribute('aria-expanded', 'true');
  });

  it('hides region content while collapsed', async () => {
    const user = userEvent.setup();
    render(<App />);

    await user.click(screen.getByRole('button', { name: 'Collapse Servers' }));

    expect(screen.queryByText('No servers yet.')).not.toBeInTheDocument();
  });
});

describe('workspace tabs', () => {
  it('starts on the workspace tab and switches to empty-state tabs', async () => {
    const user = userEvent.setup();
    render(<App />);
    const workspaceTab = screen.getByRole('tab', { name: 'Workspace' });
    expect(workspaceTab).toHaveAttribute('aria-selected', 'true');

    await user.click(screen.getByRole('tab', { name: 'Docker' }));

    expect(screen.getByRole('tab', { name: 'Docker' })).toHaveAttribute('aria-selected', 'true');
    expect(workspaceTab).toHaveAttribute('aria-selected', 'false');
    expect(screen.getByRole('tabpanel', { name: 'Docker' })).toBeVisible();
    expect(screen.getByText(/manage Docker/)).toBeVisible();
    expect(document.getElementById('panel-workspace')).not.toBeVisible();
  });

  it.each(['Editor', 'Docker', 'Diagnostics', 'History'])('offers the %s tab', async (title) => {
    const user = userEvent.setup();
    render(<App />);

    await user.click(screen.getByRole('tab', { name: title }));

    expect(screen.getByRole('tabpanel', { name: title })).toBeVisible();
  });

  it('returns to the workspace tab', async () => {
    const user = userEvent.setup();
    render(<App />);

    await user.click(screen.getByRole('tab', { name: 'History' }));
    await user.click(screen.getByRole('tab', { name: 'Workspace' }));

    expect(screen.getByRole('tabpanel', { name: 'Workspace' })).toBeVisible();
  });
});
