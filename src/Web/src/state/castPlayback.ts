import type { CastEvent } from '../api/recordings';

export const IDLE_LIMIT_SECONDS = 2;

export type PlaybackState = 'playing' | 'paused' | 'finished';

export interface PlaybackScheduler {
  now: () => number;
  setTimeout: (callback: () => void, delayMilliseconds: number) => ReturnType<typeof setTimeout>;
  clearTimeout: (handle: ReturnType<typeof setTimeout>) => void;
}

export interface PlaybackCallbacks {
  write: (data: string) => void;
  reset: () => void;
  onStateChange: (state: PlaybackState) => void;
}

const MILLISECONDS_PER_SECOND = 1000;

const browserScheduler: PlaybackScheduler = {
  now: () => performance.now(),
  setTimeout: (callback, delay) => globalThis.setTimeout(callback, delay),
  clearTimeout: (handle) => globalThis.clearTimeout(handle),
};

export function limitIdleTime(events: readonly CastEvent[], limitSeconds: number = IDLE_LIMIT_SECONDS): CastEvent[] {
  const limited: CastEvent[] = [];
  let previousOriginal = 0;
  let previousLimited = 0;
  for (const event of events) {
    const gap = Math.max(0, event.time - previousOriginal);
    const time = previousLimited + Math.min(gap, limitSeconds);
    limited.push({ time, data: event.data });
    previousOriginal = event.time;
    previousLimited = time;
  }
  return limited;
}

export class CastPlayback {
  private readonly events: CastEvent[];
  private readonly callbacks: PlaybackCallbacks;
  private readonly scheduler: PlaybackScheduler;
  private index = 0;
  private positionSeconds = 0;
  private resumedAt = 0;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private currentState: PlaybackState = 'paused';

  constructor(events: readonly CastEvent[], callbacks: PlaybackCallbacks, scheduler: PlaybackScheduler = browserScheduler) {
    this.events = limitIdleTime(events);
    this.callbacks = callbacks;
    this.scheduler = scheduler;
  }

  get state(): PlaybackState {
    return this.currentState;
  }

  get durationSeconds(): number {
    return this.events.at(-1)?.time ?? 0;
  }

  play(): void {
    if (this.currentState === 'playing') {
      return;
    }
    if (this.currentState === 'finished') {
      this.rewind();
    }
    this.resumedAt = this.scheduler.now();
    this.setState('playing');
    this.advance();
  }

  pause(): void {
    if (this.currentState !== 'playing') {
      return;
    }
    this.positionSeconds = this.elapsedSeconds();
    this.cancelTimer();
    this.setState('paused');
  }

  restart(): void {
    this.cancelTimer();
    this.rewind();
    this.currentState = 'paused';
    this.play();
  }

  dispose(): void {
    this.cancelTimer();
  }

  private rewind(): void {
    this.index = 0;
    this.positionSeconds = 0;
    this.callbacks.reset();
  }

  private advance(): void {
    this.timer = null;
    const elapsed = this.elapsedSeconds();
    while (this.index < this.events.length && this.events[this.index]!.time <= elapsed) {
      this.callbacks.write(this.events[this.index]!.data);
      this.index += 1;
    }
    const next = this.events[this.index];
    if (next === undefined) {
      this.positionSeconds = this.durationSeconds;
      this.setState('finished');
      return;
    }
    const delay = (next.time - elapsed) * MILLISECONDS_PER_SECOND;
    this.timer = this.scheduler.setTimeout(() => this.advance(), Math.max(0, delay));
  }

  private elapsedSeconds(): number {
    if (this.currentState !== 'playing') {
      return this.positionSeconds;
    }
    return this.positionSeconds + (this.scheduler.now() - this.resumedAt) / MILLISECONDS_PER_SECOND;
  }

  private cancelTimer(): void {
    if (this.timer !== null) {
      this.scheduler.clearTimeout(this.timer);
      this.timer = null;
    }
  }

  private setState(state: PlaybackState): void {
    this.currentState = state;
    this.callbacks.onStateChange(state);
  }
}
