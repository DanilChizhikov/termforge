import { breadcrumbsOf } from '../api/files';

export interface FileBreadcrumbsProps {
  path: string;
  onNavigate: (path: string) => void;
}

export function FileBreadcrumbs({ path, onNavigate }: FileBreadcrumbsProps) {
  const crumbs = breadcrumbsOf(path);
  return (
    <nav aria-label="Breadcrumbs" className="file-breadcrumbs">
      {crumbs.map((crumb) => (
        <button key={crumb.path} type="button" onClick={() => onNavigate(crumb.path)}>
          {crumb.label}
        </button>
      ))}
    </nav>
  );
}
