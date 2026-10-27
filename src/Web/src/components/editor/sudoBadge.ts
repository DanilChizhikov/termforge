import type { FileAccess } from '../../api/sudoEditor';

export function ownerLabelOf(access: FileAccess): string {
  return access.ownerName ?? `uid ${access.ownerId}`;
}

export function sudoBadgeText(access: FileAccess | null, elevated: boolean): string | null {
  if (elevated) {
    return access === null ? 'editing with sudo' : `owned by ${ownerLabelOf(access)} · editing with sudo`;
  }
  if (access === null || access.writable) {
    return null;
  }
  return `owned by ${ownerLabelOf(access)} · save needs sudo`;
}
