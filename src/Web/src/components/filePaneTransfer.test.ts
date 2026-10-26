import { describe, expect, it } from 'vitest';
import { dragMimeType, hasDraggedPaths, readDraggedPaths, writeDraggedPaths } from './filePaneTransfer';

class FakeDataTransfer {
  private readonly data = new Map<string, string>();
  effectAllowed = 'none';

  get types(): string[] {
    return Array.from(this.data.keys());
  }

  setData(type: string, value: string): void {
    this.data.set(type, value);
  }

  getData(type: string): string {
    return this.data.get(type) ?? '';
  }
}

function makeDataTransfer(): DataTransfer {
  return new FakeDataTransfer() as unknown as DataTransfer;
}

describe('filePaneTransfer', () => {
  it('round-trips dragged paths for the matching origin', () => {
    const dataTransfer = makeDataTransfer();

    writeDraggedPaths(dataTransfer, { origin: 'local', paths: ['/a', '/b'] });

    expect(dataTransfer.effectAllowed).toBe('copy');
    expect(hasDraggedPaths(dataTransfer, 'local')).toBe(true);
    expect(readDraggedPaths(dataTransfer, 'local')).toEqual({ origin: 'local', paths: ['/a', '/b'] });
  });

  it('ignores drags from the same pane', () => {
    const dataTransfer = makeDataTransfer();

    writeDraggedPaths(dataTransfer, { origin: 'remote', paths: ['/srv/a'] });

    expect(hasDraggedPaths(dataTransfer, 'local')).toBe(false);
    expect(readDraggedPaths(dataTransfer, 'local')).toBeNull();
  });

  it('rejects malformed payloads', () => {
    const dataTransfer = makeDataTransfer();
    dataTransfer.setData(dragMimeType('local'), '{not json');

    expect(readDraggedPaths(dataTransfer, 'local')).toBeNull();
  });

  it('drops non-string paths from the payload', () => {
    const dataTransfer = makeDataTransfer();
    dataTransfer.setData(dragMimeType('remote'), JSON.stringify({ origin: 'remote', paths: ['/x', 3, null] }));

    expect(readDraggedPaths(dataTransfer, 'remote')).toEqual({ origin: 'remote', paths: ['/x'] });
  });
});
