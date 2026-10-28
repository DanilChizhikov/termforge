import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it } from 'vitest';
import {
  isConfirmationSkipped,
  reloadConfirmationPreferences,
  setConfirmationSkipped,
} from '../../state/dockerConfirmPreferences';
import { DockerConfirmationsSection } from './DockerConfirmationsSection';

afterEach(() => {
  window.localStorage.clear();
  act(() => reloadConfirmationPreferences());
});

describe('DockerConfirmationsSection', () => {
  it('turns a skipped removal confirmation back on', async () => {
    const user = userEvent.setup();
    act(() => setConfirmationSkipped('containerRemove', true));
    render(<DockerConfirmationsSection />);

    const containerSwitch = screen.getByRole('switch', { name: 'Confirm container removal' });
    expect(containerSwitch).toHaveAttribute('aria-checked', 'false');
    expect(screen.getByRole('switch', { name: 'Confirm image removal' })).toHaveAttribute('aria-checked', 'true');

    await user.click(containerSwitch);

    expect(containerSwitch).toHaveAttribute('aria-checked', 'true');
    expect(isConfirmationSkipped('containerRemove')).toBe(false);
  });
});
