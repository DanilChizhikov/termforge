import type { FileAccess } from '../../api/sudoEditor';
import { ShieldIcon } from '../icons/Icons';
import { sudoBadgeText } from './sudoBadge';

export interface SudoAccessBadgeProps {
  access: FileAccess | null;
  elevated: boolean;
}

export function SudoAccessBadge({ access, elevated }: SudoAccessBadgeProps) {
  const text = sudoBadgeText(access, elevated);
  if (text === null) {
    return null;
  }
  return (
    <span className="badge badge-caution editor-sudo-badge">
      <ShieldIcon size={11} />
      {text}
    </span>
  );
}
