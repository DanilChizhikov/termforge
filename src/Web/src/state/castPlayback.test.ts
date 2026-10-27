import { describe, expect, it } from 'vitest';
import { CastPlayback, limitIdleTime, type PlaybackScheduler, type PlaybackState } from './castPlayback';

class ManualScheduler implements PlaybackScheduler {
  private current = 0;
  private nextHandle = 1;
  private readonly pending = new Map<number, { at: number; callback: () => void }>();

  now = () => this.current;

  setTimeout = (callback: () => void, delay: number): ReturnType<typeof setTimeout> => {
    const handle = this.nextHandle++;
    this.pending.set(handle, { at: this.current + delay, callback });
    return handle as unknown as ReturnType<typeof setTimeout>;
  };

  clearTimeout = (handle: ReturnType<typeof setTimeout>): void => {
    this.pending.delete(handle as unknown as number);
  };

  advance(milliseconds: number): void {
    const target = this.current + milliseconds;
    for (;;) {
      const due = [...this.pending.entries()].filter(([, timer]) => timer.at <= target).sort((a, b) => a[1].at - b[1].at)[0];
      if (due === undefined) {
        break;
      }
      this.pending.delete(due[0]);
      this.current = due[1].at;
      due[1].callback();
    }
    this.current = target;
  }
}

function createPlayback(events: { time: number; data: string }[]) {
  const scheduler = new ManualScheduler();
  const written: string[] = [];
  const states: PlaybackState[] = [];
  let resets = 0;
  const playback = new CastPlayback(
    events,
    {
      write: (data) => written.push(data),
      reset: () => {
        resets += 1;
      },
      onStateChange: (state) => states.push(state),
    },
    scheduler,
  );
  return { playback, scheduler, written, states, resets: () => resets };
}

describe('cast playback', () => {
  it('shortens idle gaps longer than the limit', () => {
    const limited = limitIdleTime([
      { time: 0.5, data: 'a' },
      { time: 30, data: 'b' },
      { time: 30.5, data: 'c' },
    ]);

    expect(limited.map((event) => event.time)).toEqual([0.5, 2.5, 3]);
  });

  it('writes events at their recorded times and finishes', () => {
    const { playback, scheduler, written, states } = createPlayback([
      { time: 0, data: 'first' },
      { time: 1, data: 'second' },
    ]);

    playback.play();
    expect(written).toEqual(['first']);

    scheduler.advance(999);
    expect(written).toEqual(['first']);

    scheduler.advance(1);
    expect(written).toEqual(['first', 'second']);
    expect(states).toEqual(['playing', 'finished']);
    expect(playback.durationSeconds).toBe(1);
  });

  it('pauses and resumes from the same position', () => {
    const { playback, scheduler, written } = createPlayback([
      { time: 0.5, data: 'a' },
      { time: 1.5, data: 'b' },
    ]);

    playback.play();
    scheduler.advance(600);
    playback.pause();
    scheduler.advance(10_000);
    expect(written).toEqual(['a']);

    playback.play();
    scheduler.advance(899);
    expect(written).toEqual(['a']);
    scheduler.advance(1);
    expect(written).toEqual(['a', 'b']);
  });

  it('restarts from the beginning with a cleared screen', () => {
    const { playback, scheduler, written, resets } = createPlayback([{ time: 0, data: 'only' }]);

    playback.play();
    scheduler.advance(10);
    playback.restart();

    expect(resets()).toBe(1);
    expect(written).toEqual(['only', 'only']);
  });
});
