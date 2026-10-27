import type { ComponentType } from 'react';
import type { IconProps } from './icons/Icons';

export type DragOrigin = 'local' | 'remote';

export interface FilePaneTransfer {
  label: string;
  primary: boolean;
  Icon: ComponentType<IconProps>;
  canSend: boolean;
  unavailableReason: string;
  onSend: (paths: string[]) => void;
  dragOrigin: DragOrigin;
  acceptsFrom: DragOrigin;
  canReceive: boolean;
  onReceive?: (paths: string[]) => void;
}

export interface DraggedPaths {
  origin: DragOrigin;
  paths: string[];
}

const DRAG_MIME_PREFIX = 'application/x-termforge-';

export function dragMimeType(origin: DragOrigin): string {
  return `${DRAG_MIME_PREFIX}${origin}`;
}

export function hasDraggedPaths(dataTransfer: DataTransfer, origin: DragOrigin): boolean {
  return Array.from(dataTransfer.types).includes(dragMimeType(origin));
}

export function writeDraggedPaths(dataTransfer: DataTransfer, dragged: DraggedPaths): void {
  dataTransfer.setData(dragMimeType(dragged.origin), JSON.stringify(dragged));
  dataTransfer.effectAllowed = 'copy';
}

export function readDraggedPaths(dataTransfer: DataTransfer, origin: DragOrigin): DraggedPaths | null {
  const raw = dataTransfer.getData(dragMimeType(origin));
  if (raw === '') {
    return null;
  }
  try {
    const parsed = JSON.parse(raw) as Partial<DraggedPaths>;
    const validOrigin = parsed.origin === 'local' || parsed.origin === 'remote';
    if (!validOrigin || !Array.isArray(parsed.paths)) {
      return null;
    }
    const paths = parsed.paths.filter((path): path is string => typeof path === 'string');
    return { origin: parsed.origin as DragOrigin, paths };
  } catch {
    return null;
  }
}
