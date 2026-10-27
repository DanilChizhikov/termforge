import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { emptyInput } from '../api/connections';
import { ServerForm } from './ServerForm';

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
