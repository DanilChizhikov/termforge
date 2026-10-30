const METRIC_TITLES = ['Connection', 'Uptime', 'CPU', 'Memory', 'Disk'];

export function MetricCardsSkeleton() {
  return (
    <div className="metric-grid" aria-busy="true" aria-hidden="true" data-testid="metric-cards-skeleton">
      {METRIC_TITLES.map((title) => (
        <div key={title} className="metric-card">
          <span className="metric-label">{title}</span>
          <span className="skeleton-line skeleton-line-value" />
          <span className="skeleton-line skeleton-line-detail" />
        </div>
      ))}
    </div>
  );
}
