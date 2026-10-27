import { LockIcon } from './icons/Icons';

export function ReadOnlyBadge() {
  return (
    <span className="badge badge-caution read-only-badge">
      <LockIcon size={10} />
      read-only
    </span>
  );
}
