import { useTransfersState } from '../state/transfers';
import { activateWorkspaceTab } from '../state/workspaceTab';

export function TransfersIndicator() {
  const { transfers } = useTransfersState();
  const running = transfers.filter((transfer) => transfer.status === 'running').length;

  if (running === 0) {
    return null;
  }

  return (
    <button type="button" className="header-link" onClick={() => activateWorkspaceTab('history')}>
      <span className="status-dot status-dot-active" aria-hidden="true" />
      {running === 1 ? '1 transfer running' : `${running} transfers running`}
    </button>
  );
}
