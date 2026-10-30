import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { LoadingState } from './LoadingState';

describe('LoadingState', () => {
  it('shows the message next to a decorative spinner', () => {
    const { container } = render(<LoadingState message="Checking server…" />);

    const message = screen.getByText('Checking server…');
    const spinner = container.querySelector('svg.spinner');
    expect(message).toHaveClass('loading-state');
    expect(spinner).toHaveAttribute('aria-hidden', 'true');
  });

  it('keeps the layout class of the place it replaces', () => {
    render(<LoadingState message="Loading..." className="empty-state" />);

    expect(screen.getByText('Loading...')).toHaveClass('loading-state', 'empty-state');
  });
});
