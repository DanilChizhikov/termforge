import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Group, Panel } from 'react-resizable-panels';
import { beforeEach, describe, expect, it } from 'vitest';
import { resetWorkspaceLayout, restoreWorkspaceLayout } from '../state/workspaceLayout';
import { CollapsibleRegion, RegionToggle, useRegionToggleState } from './CollapsibleRegion';

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

function renderContentRegion() {
  return render(
    <Group orientation="horizontal" id="content-test">
      <CollapsibleRegion id="servers" title="Servers" defaultSize="30%" minSize="10%" collapsedLayout="content">
        <CollapsedAwareContent />
      </CollapsibleRegion>
      <Panel id="rest" minSize="30%" />
    </Group>,
  );
}

function CollapsedAwareContent() {
  const region = useRegionToggleState();
  return (
    <>
      <p>{region?.collapsed === true ? 'Compact view' : 'Full view'}</p>
      <RegionToggle />
    </>
  );
}

describe('CollapsibleRegion content layout', () => {
  it('keeps the content visible and lets it render the collapsed state', async () => {
    restoreWorkspaceLayout([], ['servers']);
    const user = userEvent.setup();
    renderContentRegion();

    const region = screen.getByRole('region', { name: 'Servers' });
    expect(within(region).getByText('Compact view')).toBeVisible();
    expect(within(region).queryByText('Servers')).not.toBeInTheDocument();

    await user.click(within(region).getByRole('button', { name: 'Expand Servers' }));

    expect(within(region).getByText('Full view')).toBeVisible();
  });
});
