import type { CpuSample } from '../state/serverMetrics';

export interface CpuSparklineProps {
  samples: readonly CpuSample[];
}

const VIEW_WIDTH = 200;
const VIEW_HEIGHT = 28;
const VERTICAL_PADDING = 2;
const MILLISECONDS_PER_MINUTE = 60_000;

function toY(percent: number): number {
  const clamped = Math.min(100, Math.max(0, percent));
  const drawableHeight = VIEW_HEIGHT - VERTICAL_PADDING * 2;
  return VERTICAL_PADDING + (1 - clamped / 100) * drawableHeight;
}

export function sparklinePoints(samples: readonly CpuSample[]): string {
  if (samples.length === 0) {
    return '';
  }
  const times = samples.map((sample) => Date.parse(sample.at));
  const first = times[0] ?? 0;
  const span = (times[times.length - 1] ?? first) - first;
  if (span <= 0) {
    const y = toY(samples[samples.length - 1]?.percent ?? 0).toFixed(1);
    return `0,${y} ${VIEW_WIDTH},${y}`;
  }
  return samples
    .map((sample, index) => {
      const x = (((times[index] ?? first) - first) / span) * VIEW_WIDTH;
      return `${x.toFixed(1)},${toY(sample.percent).toFixed(1)}`;
    })
    .join(' ');
}

export function describeSparkline(samples: readonly CpuSample[]): string {
  const percents = samples.map((sample) => Math.round(sample.percent));
  const lowest = Math.min(...percents);
  const highest = Math.max(...percents);
  const first = Date.parse(samples[0]?.at ?? '');
  const last = Date.parse(samples[samples.length - 1]?.at ?? '');
  const minutes = Math.max(1, Math.round((last - first) / MILLISECONDS_PER_MINUTE));
  const period = minutes === 1 ? 'the last minute' : `the last ${minutes} minutes`;
  if (lowest === highest) {
    return `CPU usage over ${period}, ${lowest} percent`;
  }
  return `CPU usage over ${period}, between ${lowest} and ${highest} percent`;
}

export function CpuSparkline({ samples }: CpuSparklineProps) {
  if (samples.length === 0) {
    return null;
  }
  return (
    <svg
      className="sparkline"
      role="img"
      aria-label={describeSparkline(samples)}
      viewBox={`0 0 ${VIEW_WIDTH} ${VIEW_HEIGHT}`}
      preserveAspectRatio="none"
    >
      <polyline className="sparkline-line" points={sparklinePoints(samples)} />
    </svg>
  );
}
