import type { FloatingRect } from './floatingGeometry';

interface FloatingPlacementRequest {
  preferred: FloatingRect;
  viewport: FloatingRect | undefined;
  occupied: readonly FloatingRect[];
}

interface SearchArea {
  left: number;
  right: number;
  firstY: number;
  lastY: number;
}

interface Candidate {
  rect: FloatingRect;
  distance: number;
  direction: number;
}

const UNMEASURED_VIEWPORT = { x: 0, y: 0, width: Infinity, height: Infinity };

export function placeFloatingRect({
  preferred, viewport = UNMEASURED_VIEWPORT, occupied,
}: FloatingPlacementRequest): FloatingRect | null {
  const obstacles = [...occupied].sort((a, b) => a.x - b.x);
  const bottom = viewport.y + viewport.height;
  const lastY = bottom - preferred.height;
  const firstY = Math.max(viewport.y, Math.min(preferred.y, lastY));
  const right = viewport.x + Math.max(preferred.width, viewport.width);
  const fitsViewport = preferred.width <= viewport.width && preferred.height <= viewport.height;

  if (fitsViewport) {
    const visible = { left: viewport.x, right, firstY, lastY };
    const placed = findFreePosition(preferred, visible, obstacles);
    if (placed !== null) return placed;
  }

  const overflow = { left: viewport.x, right, firstY: fitsViewport ? bottom : firstY, lastY: Infinity };
  return findFreePosition(preferred, overflow, obstacles);
}

function findFreePosition(preferred: FloatingRect, area: SearchArea, obstacles: readonly FloatingRect[]): FloatingRect | null {
  const rows = new Set([area.firstY]);
  for (const obstacle of obstacles) {
    const bottom = obstacle.y + obstacle.height;
    if (bottom > area.firstY && bottom <= area.lastY) rows.add(bottom);
  }

  let best: Candidate | null = null;
  for (const y of [...rows].sort((a, b) => a - b)) {
    const verticalDistance = (y - preferred.y) ** 2;
    if (best !== null && y >= preferred.y && verticalDistance > best.distance) break;

    const row = { x: area.left, y, width: area.right - area.left, height: preferred.height };
    for (const gap of freeGaps(row, obstacles)) {
      if (gap.right - gap.left < preferred.width) continue;
      const x = Math.min(Math.max(preferred.x, gap.left), gap.right - preferred.width);
      const candidate = {
        rect: { ...preferred, x, y },
        distance: (x - preferred.x) ** 2 + verticalDistance,
        direction: y > area.firstY ? 2 : x >= preferred.x ? 0 : 1,
      };
      if (best === null || compareCandidates(candidate, best) < 0) best = candidate;
    }
  }
  return best?.rect ?? null;
}

function* freeGaps(row: FloatingRect, obstacles: readonly FloatingRect[]) {
  let left = row.x;
  const right = row.x + row.width;
  for (const obstacle of obstacles) {
    if (obstacle.x >= right) break;
    const end = obstacle.x + obstacle.width;
    if (end <= left || obstacle.y >= row.y + row.height || obstacle.y + obstacle.height <= row.y) continue;
    if (obstacle.x > left) yield { left, right: obstacle.x };
    left = end;
    if (left >= right) return;
  }
  yield { left, right };
}

function compareCandidates(a: Candidate, b: Candidate): number {
  return a.distance - b.distance || a.direction - b.direction || a.rect.y - b.rect.y || a.rect.x - b.rect.x;
}
