import { Fragment } from 'react';
import { breadcrumbsOf } from '../api/files';

export interface FileBreadcrumbsProps {
  path: string;
  onNavigate: (path: string) => void;
}

function endsWithSeparator(label: string): boolean {
  return label.endsWith('/') || label.endsWith('\\');
}

export function FileBreadcrumbs({ path, onNavigate }: FileBreadcrumbsProps) {
  const crumbs = breadcrumbsOf(path);
  const separator = path.includes('\\') ? '\\' : '/';
  return (
    <nav aria-label="Breadcrumbs" className="file-breadcrumbs">
      {crumbs.map((crumb, index) => {
        const previous = index > 0 ? crumbs[index - 1] : undefined;
        const showSeparator = previous !== undefined && !endsWithSeparator(previous.label);
        return (
          <Fragment key={crumb.path}>
            {showSeparator ? (
              <span className="crumb-separator" aria-hidden="true">
                {separator}
              </span>
            ) : null}
            <button type="button" className="crumb" onClick={() => onNavigate(crumb.path)}>
              {crumb.label}
            </button>
          </Fragment>
        );
      })}
    </nav>
  );
}
