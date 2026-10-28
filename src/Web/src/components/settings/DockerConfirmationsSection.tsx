import { setConfirmationSkipped, useSkippedConfirmations, type SkippableConfirmation } from '../../state/dockerConfirmPreferences';
import { Switch } from '../Switch';

interface ConfirmationRow {
  kind: SkippableConfirmation;
  title: string;
  hint: string;
}

const ROWS: readonly ConfirmationRow[] = [
  {
    kind: 'containerRemove',
    title: 'Confirm container removal',
    hint: 'Ask before a Docker container is removed.',
  },
  {
    kind: 'imageRemove',
    title: 'Confirm image removal',
    hint: 'Ask before a Docker image is removed.',
  },
];

export function DockerConfirmationsSection() {
  const skipped = useSkippedConfirmations();

  return (
    <section className="settings-section" aria-labelledby="settings-docker-confirmations-title">
      <div className="settings-section-header">
        <div className="settings-row-text">
          <h2 id="settings-docker-confirmations-title" className="settings-section-title">
            Docker confirmations
          </h2>
          <span className="settings-row-hint">
            Turned off by &quot;Don&apos;t ask again&quot;. Stop, restart and compose actions always ask. Stored in this browser.
          </span>
        </div>
      </div>
      <div className="settings-card">
        {ROWS.map((row) => (
          <div key={row.kind} className="settings-row">
            <div className="settings-row-text">
              <span className="settings-row-title">{row.title}</span>
              <span className="settings-row-hint">{row.hint}</span>
            </div>
            <Switch
              label={row.title}
              checked={!skipped[row.kind]}
              onChange={(checked) => setConfirmationSkipped(row.kind, !checked)}
            />
          </div>
        ))}
      </div>
    </section>
  );
}
