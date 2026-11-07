import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it } from 'vitest';
import type { SavedConnection } from '../api/connections';
import { replaceConnections } from '../state/connections';
import { getNetworkDiagnostics, resetNetworkDiagnostics, updateDiagnosticDraft } from '../state/networkDiagnostics';
import { NetworkMeasurementPoints } from './NetworkMeasurementPoints';

beforeEach(() => {
  resetNetworkDiagnostics();
  replaceConnections(Array.from({ length: 200 }, (_, i) => ({
    id: `server-${i}`, name: `Server ${i}`, host: `node-${i}.example.net`, groupName: i % 2 === 0 ? 'Production' : 'Staging',
  } as SavedConnection)));
});

describe('measurement point selection', () => {
  it('shows selected points only and searches all servers by name, host and group', async () => {
    const user = userEvent.setup();
    render(<NetworkMeasurementPoints disabled={false} />);
    expect(screen.queryByText('Server 199')).not.toBeInTheDocument();
    expect(screen.getByLabelText('Category for Local machine')).toHaveTextContent('RussiaOutside Russia');
    await user.click(screen.getByRole('button', { name: 'Choose measurement points…' }));
    const search = screen.getByLabelText('Search servers');
    await user.type(search, 'Server 199');
    expect(screen.getAllByRole('checkbox')).toHaveLength(1);
    await user.clear(search);
    await user.type(search, 'node-198.example.net');
    expect(screen.getByRole('checkbox', { name: /Server 198/ })).toBeInTheDocument();
    await user.clear(search);
    await user.type(search, 'production');
    expect(screen.getAllByRole('checkbox')).toHaveLength(100);
    await user.clear(search);
    await user.type(search, 'missing-server');
    expect(screen.getByText('No matching points.')).toBeVisible();
  });

  it('counts the local machine toward the limit and applies the temporary selection', async () => {
    const user = userEvent.setup();
    render(<NetworkMeasurementPoints disabled={false} />);
    await user.click(screen.getByRole('button', { name: 'Choose measurement points…' }));
    for (let i = 0; i < 7; i++) await user.click(screen.getByRole('checkbox', { name: new RegExp(`^Server ${i}\\b`) }));
    expect(screen.getByRole('status')).toHaveTextContent('8 / 8 selected · Limit reached');
    expect(screen.getByRole('checkbox', { name: /^Server 7\b/ })).toBeDisabled();
    expect(screen.getByRole('checkbox', { name: /^Local machine/ })).toBeEnabled();
    expect(getNetworkDiagnostics().draft.points).toHaveLength(1);
    await user.click(screen.getByRole('button', { name: 'Apply selection' }));
    expect(getNetworkDiagnostics().draft.points).toHaveLength(8);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('Category for Server 0'), 'outsideRf');
    expect(getNetworkDiagnostics().draft.points[1]?.category).toBe('outsideRf');
    await user.click(screen.getByRole('button', { name: 'Remove Server 0' }));
    expect(getNetworkDiagnostics().draft.points).toHaveLength(7);
  });

  it('keeps categories and discards changes on Cancel or Escape, traps and restores focus', async () => {
    const user = userEvent.setup();
    updateDiagnosticDraft({ points: [{ connectionId: 'server-0', category: 'outsideRf' }] });
    render(<NetworkMeasurementPoints disabled={false} />);
    const trigger = screen.getByRole('button', { name: 'Choose measurement points…' });
    await user.click(trigger);
    expect(screen.getByLabelText('Search servers')).toHaveFocus();
    await user.tab({ shift: true });
    expect(screen.getByRole('button', { name: 'Apply selection' })).toHaveFocus();
    await user.tab();
    expect(screen.getByLabelText('Search servers')).toHaveFocus();
    await user.click(screen.getByRole('checkbox', { name: /^Local machine/ }));
    await user.keyboard('{Escape}');
    expect(trigger).toHaveFocus();
    expect(getNetworkDiagnostics().draft.points).toEqual([{ connectionId: 'server-0', category: 'outsideRf' }]);
    await user.click(trigger);
    await user.click(screen.getByRole('checkbox', { name: /^Server 0\b/ }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Cancel' }));
    expect(getNetworkDiagnostics().draft.points).toEqual([{ connectionId: 'server-0', category: 'outsideRf' }]);
    await user.click(trigger);
    await user.click(screen.getByRole('button', { name: 'Apply selection' }));
    expect(screen.getByLabelText('Category for Server 0')).toHaveValue('outsideRf');
  });

  it('disables editing during checks', () => {
    render(<NetworkMeasurementPoints disabled />);
    expect(screen.getByRole('button', { name: 'Choose measurement points…' })).toBeDisabled();
    expect(screen.getByLabelText('Category for Local machine')).toBeDisabled();
  });
});
