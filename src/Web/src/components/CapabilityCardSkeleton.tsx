const SKELETON_ROW_KEYS = ['first', 'second', 'third', 'fourth'];

export function CapabilityCardSkeleton() {
  return (
    <div className="capability-card" aria-busy="true" aria-hidden="true" data-testid="capability-card-skeleton">
      <div className="capability-card-header">
        <span className="capability-card-title">Capabilities</span>
      </div>
      <ul className="capability-list">
        {SKELETON_ROW_KEYS.map((key) => (
          <li key={key} className="capability-row">
            <span className="skeleton-line skeleton-line-icon" />
            <span className="skeleton-line skeleton-line-name" />
            <span className="skeleton-line skeleton-line-detail" />
          </li>
        ))}
      </ul>
    </div>
  );
}
