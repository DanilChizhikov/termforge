import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { OperationRecord } from '../api/operations';
import { OperationTable } from './OperationTable';

function operation(overrides: Partial<OperationRecord>): OperationRecord {
  return {
    id: 'empty', kind: 'file.save', server: 'server', status: 'failed',
    createdAt: '2026-11-07T00:00:00Z', startedAt: null, finishedAt: null,
    updatedAt: '2026-11-07T00:00:00Z', details: {}, error: null, retryable: false,
    ...overrides,
  };
}

describe('OperationTable', () => {
  it('keeps six accessible cells for empty and multiline details', () => {
    const path = `/home/${'directory/'.repeat(40)}file\nsecond line`;
    const error = `Permission denied\n${'long-error'.repeat(100)}`;
    const operations = [operation({}), operation({ id: 'long', details: { path }, error })];

    render(<OperationTable operations={operations} onRetry={vi.fn()} />);

    const rows = screen.getAllByRole('row').slice(1);
    for (const row of rows) expect(within(row).getAllByRole('cell')).toHaveLength(6);
    const details = within(rows[1]!).getAllByRole('cell')[3]!;
    expect(details.querySelector('code')?.textContent).toBe(path);
    expect(details.querySelector('.operation-error')?.textContent).toBe(error);
    expect(details.querySelector('.operation-details-content')).not.toBeNull();
  });

  it('keeps retry and discard reachable by keyboard', async () => {
    const item = operation({ retryable: true, hasPartial: true });
    const retry = vi.fn();
    const discard = vi.fn();
    const user = userEvent.setup();
    render(<OperationTable operations={[item]} onRetry={retry} onDiscard={discard} />);

    await user.tab();
    expect(screen.getByRole('button', { name: 'Retry' })).toHaveFocus();
    await user.keyboard('{Enter}');
    expect(retry).toHaveBeenCalledWith(item);
    await user.tab();
    await user.keyboard('{Enter}');
    expect(discard).toHaveBeenCalledWith(item);
  });
});
