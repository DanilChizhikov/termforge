import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import * as api from '../api/connections';
import { ApiError } from '../api/client';
import { emptyInput } from '../api/connections';
import { ServerForm } from './ServerForm';

vi.mock('../api/connections', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/connections')>();
  return { ...actual, revealPassword: vi.fn() };
});

function renderForm(onSubmit = vi.fn(() => Promise.resolve())) {
  render(
    <ServerForm
      title="Add server"
      initial={{ ...emptyInput(), name: 'web', host: 'h', userName: 'u' }}
      editingId={null}
      connections={[]}
      hasStoredPassword={false}
      onSubmit={onSubmit}
      onCancel={vi.fn()}
    />,
  );
  return onSubmit;
}

describe('ServerForm reconnect policy', () => {
  it('defaults to manual reconnect and hides the backoff fields', () => {
    renderForm();

    expect(screen.getByLabelText(/If the connection drops/)).toHaveValue('Manual');
    expect(screen.queryByLabelText('Attempts before giving up')).not.toBeInTheDocument();
    expect(screen.getByText(/never re-runs the command/)).toBeInTheDocument();
  });

  it('shows and submits the backoff settings for automatic reconnect', async () => {
    const onSubmit = renderForm();

    await userEvent.selectOptions(screen.getByLabelText(/If the connection drops/), 'Automatic');
    const attempts = screen.getByLabelText('Attempts before giving up');
    await userEvent.clear(attempts);
    await userEvent.type(attempts, '8');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ reconnectMode: 'Automatic', reconnectMaxAttempts: 8, reconnectInitialDelaySeconds: 2 }),
    );
  });

  it('can turn reconnect off', async () => {
    const onSubmit = renderForm();

    await userEvent.selectOptions(screen.getByLabelText(/If the connection drops/), 'Off');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ reconnectMode: 'Off' }));
  });
});

describe('ServerForm read-only mode', () => {
  it('is off by default and submits the read-only flag when turned on', async () => {
    const onSubmit = renderForm();
    const toggle = screen.getByRole('switch', { name: 'Read-only server' });
    expect(toggle).toHaveAttribute('aria-checked', 'false');
    expect(screen.getByText('Block uploads, saves and Docker actions on this server')).toBeInTheDocument();

    await userEvent.click(toggle);
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ readOnly: true }));
  });
});

function renderPasswordForm(hasStoredPassword: boolean, onSubmit = vi.fn(() => Promise.resolve())) {
  render(
    <ServerForm
      title="Edit server"
      initial={{ ...emptyInput(), name: 'db', host: 'h', userName: 'u', authMethod: 'Password' }}
      editingId="c1"
      connections={[]}
      hasStoredPassword={hasStoredPassword}
      onSubmit={onSubmit}
      onCancel={vi.fn()}
    />,
  );
}

describe('ServerForm password visibility', () => {
  beforeEach(() => {
    vi.mocked(api.revealPassword).mockReset();
  });

  it('shows and hides a typed password without loading the stored one', async () => {
    renderPasswordForm(false);
    const field = screen.getByLabelText('Server password');
    await userEvent.type(field, 'typed-secret');

    await userEvent.click(screen.getByRole('button', { name: 'Show password' }));
    expect(field).toHaveAttribute('type', 'text');
    await userEvent.click(screen.getByRole('button', { name: 'Hide password' }));

    expect(field).toHaveAttribute('type', 'password');
    expect(api.revealPassword).not.toHaveBeenCalled();
  });

  it('masks a stored password and keeps it read-only until it is shown', async () => {
    const onSubmit = vi.fn(() => Promise.resolve());
    renderPasswordForm(true, onSubmit);
    const field = screen.getByLabelText('Server password');

    await userEvent.type(field, 'typed');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));

    expect(field).toHaveAttribute('type', 'password');
    expect(field).toHaveAttribute('readonly');
    expect(field).toHaveValue('********');
    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ password: '' }));
  });

  it('makes the stored password editable after it is shown', async () => {
    vi.mocked(api.revealPassword).mockResolvedValueOnce('stored-secret');
    renderPasswordForm(true);

    await userEvent.click(screen.getByRole('button', { name: 'Show password' }));
    const field = await screen.findByDisplayValue('stored-secret');
    await userEvent.type(field, '-2');

    expect(field).not.toHaveAttribute('readonly');
    expect(field).toHaveValue('stored-secret-2');
  });

  it('loads and shows the stored password', async () => {
    vi.mocked(api.revealPassword).mockResolvedValueOnce('stored-secret');
    renderPasswordForm(true);

    await userEvent.click(screen.getByRole('button', { name: 'Show password' }));

    const field = await screen.findByDisplayValue('stored-secret');
    expect(field).toHaveAttribute('type', 'text');
    expect(api.revealPassword).toHaveBeenCalledWith('c1');
  });

  it('reports a stored password that cannot be read', async () => {
    const failure = new ApiError(423, { detail: 'The vault is locked.' });
    vi.mocked(api.revealPassword).mockRejectedValueOnce(failure);
    renderPasswordForm(true);

    await userEvent.click(screen.getByRole('button', { name: 'Show password' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('The vault is locked.');
    expect(screen.getByLabelText('Server password')).toHaveAttribute('type', 'password');
  });
});
