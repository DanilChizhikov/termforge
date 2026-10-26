import type { ReactNode } from 'react';

export interface IconProps {
  size?: number;
}

interface IconFrameProps {
  size: number;
  strokeWidth: number;
  children: ReactNode;
}

function IconFrame({ size, strokeWidth, children }: IconFrameProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
    >
      {children}
    </svg>
  );
}

export function BrandMarkIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2.6}>
      <path d="M5 17l6-5-6-5" />
      <path d="M13 19h7" />
    </IconFrame>
  );
}

export function TerminalIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M4 17l6-5-6-5" />
      <path d="M12 19h8" />
    </IconFrame>
  );
}

export function FileIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M14 3H6a1 1 0 0 0-1 1v16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V8z" />
      <path d="M14 3v5h5" />
    </IconFrame>
  );
}

export function FolderIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z" />
    </IconFrame>
  );
}

export function LinkIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1" />
      <path d="M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1" />
    </IconFrame>
  );
}

export function ContainerIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M21 8l-9-5-9 5 9 5 9-5z" />
      <path d="M3 8v8l9 5 9-5V8" />
      <path d="M12 13v8" />
    </IconFrame>
  );
}

export function PulseIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M3 12h4l3 7 4-14 3 7h4" />
    </IconFrame>
  );
}

export function ListIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01" />
    </IconFrame>
  );
}

export function ChevronDownIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2.4}>
      <path d="M6 9l6 6 6-6" />
    </IconFrame>
  );
}

export function ChevronRightIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2.4}>
      <path d="M9 6l6 6-6 6" />
    </IconFrame>
  );
}

export function SortAscendingIcon({ size = 11 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2.4}>
      <path d="M12 19V5M6 11l6-6 6 6" />
    </IconFrame>
  );
}

export function SortDescendingIcon({ size = 11 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2.4}>
      <path d="M12 5v14M6 13l6 6 6-6" />
    </IconFrame>
  );
}

export function PlusIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M12 5v14M5 12h14" />
    </IconFrame>
  );
}

export function CloseIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M6 6l12 12M18 6L6 18" />
    </IconFrame>
  );
}

export function ArrowUpIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2.2}>
      <path d="M12 19V5M6 11l6-6 6 6" />
    </IconFrame>
  );
}

export function RefreshIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M20 11a8 8 0 0 0-14.3-4.9L4 8" />
      <path d="M4 4v4h4" />
      <path d="M4 13a8 8 0 0 0 14.3 4.9L20 16" />
      <path d="M20 20v-4h-4" />
    </IconFrame>
  );
}

export function ImportIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M12 5v14M6 13l6 6 6-6" />
    </IconFrame>
  );
}

export function ShieldIcon({ size = 18 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M12 3l8 3v6c0 4.5-3.4 8.2-8 9-4.6-.8-8-4.5-8-9V6z" />
    </IconFrame>
  );
}

export function ShieldAlertIcon({ size = 18 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M12 3l8 3v6c0 4.5-3.4 8.2-8 9-4.6-.8-8-4.5-8-9V6z" />
      <path d="M12 8v5M12 16h.01" />
    </IconFrame>
  );
}

export function LockIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <rect x="5" y="11" width="14" height="10" rx="2" />
      <path d="M8 11V8a4 4 0 0 1 8 0v3" />
    </IconFrame>
  );
}

export function PinIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M9 4h6l-1 6 4 4H6l4-4z" />
      <path d="M12 14v6" />
    </IconFrame>
  );
}

export function InsertIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M5 12h14" />
      <path d="M13 6l6 6-6 6" />
    </IconFrame>
  );
}

export function TrashIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3" />
    </IconFrame>
  );
}

export function ArrowDownIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2.4}>
      <path d="M12 5v14M6 13l6 6 6-6" />
    </IconFrame>
  );
}

export function ArrowRightIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2.4}>
      <path d="M5 12h14M13 6l6 6-6 6" />
    </IconFrame>
  );
}

export function ArrowLeftIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2.4}>
      <path d="M19 12H5M11 6l-6 6 6 6" />
    </IconFrame>
  );
}

export function SettingsIcon({ size = 18 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={1.8}>
      <path d="M4 6h9M17 6h3M4 12h3M11 12h9M4 18h11M19 18h1" />
      <circle cx="15" cy="6" r="2" />
      <circle cx="9" cy="12" r="2" />
      <circle cx="17" cy="18" r="2" />
    </IconFrame>
  );
}

export function ChevronLeftIcon({ size = 13 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M15 6l-6 6 6 6" />
    </IconFrame>
  );
}

export function PlayIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M7 5l12 7-12 7z" />
    </IconFrame>
  );
}

export function PauseIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <path d="M8 5v14M16 5v14" />
    </IconFrame>
  );
}

export function SearchIcon({ size = 14 }: IconProps) {
  return (
    <IconFrame size={size} strokeWidth={2}>
      <circle cx="11" cy="11" r="7" />
      <path d="M20 20l-3.5-3.5" />
    </IconFrame>
  );
}
