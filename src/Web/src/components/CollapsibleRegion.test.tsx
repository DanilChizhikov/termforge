import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Group, Panel } from 'react-resizable-panels';
import { beforeEach, describe, expect, it } from 'vitest';
import { resetWorkspaceLayout, restoreWorkspaceLayout } from '../state/workspaceLayout';
import { CollapsibleRegion, RegionToggle } from './CollapsibleRegion';

function renderRailRegion() {
  return render(
    <Group orientation="horizontal" id="rail-test">
      <CollapsibleRegion id="logs" title="Logs" defaultSize="30%" minSize="10%" collapsedLayout="rail">
        <p>Log lines</p>
        <RegionToggle />
      </CollapsibleRegion>
      <Panel id="rest" minSize="30%" />
    </Group>,
  );
}

beforeEach(() => {
  resetWorkspaceLayout();
});

describe('CollapsibleRegion rail layout', () => {
  it('shows the expand button and the title in the collapsed rail', () => {
    restoreWorkspaceLayout([], ['logs']);

    renderRailRegion();

    const region = screen.getByRole('region', { name: 'Logs' });
    expect(within(region).getByRole('button', { name: 'Expand Logs' })).toHaveAttribute('aria-expanded', 'false');
    expect(within(region).getByText('Logs')).toHaveClass('region-rail-label');
    expect(within(region).queryByText('Log lines')).not.toBeInTheDocument();
  });

  it('expands the region from the rail', async () => {
    restoreWorkspaceLayout([], ['logs']);
    const user = userEvent.setup();
    renderRailRegion();

    await user.click(screen.getByRole('button', { name: 'Expand Logs' }));

    const region = screen.getByRole('region', { name: 'Logs' });
    expect(within(region).getByText('Log lines')).toBeInTheDocument();
    expect(within(region).getByRole('button', { name: 'Collapse Logs' })).toHaveAttribute('aria-expanded', 'true');
  });
});
