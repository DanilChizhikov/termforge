import { useConnection } from '../state/connections';
import { LockIcon } from './icons/Icons';

export interface TerminalReadOnlyBannerProps {
  connectionId: string;
}

export function TerminalReadOnlyBanner({ connectionId }: TerminalReadOnlyBannerProps) {
  const connection = useConnection(connectionId);
  if (connection === null || !connection.readOnly) {
    return null;
  }
  return (
    <p role="note" className="terminal-read-only">
      <LockIcon size={13} />
      <span>
        <strong>{connection.name} is read-only.</strong> Uploads, saves and Docker actions are blocked. Commands typed
        in this shell still run with the SSH user&apos;s rights.
      </span>
    </p>
  );
}
