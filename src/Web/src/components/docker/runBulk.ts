import { describeError } from '../../api/connections';
import type { ActionNotice } from './DockerNotice';

export interface BulkFailure {
  key: string;
  message: string;
}

export interface BulkOutcome {
  succeeded: string[];
  failed: BulkFailure[];
}

export interface BulkWording {
  pastTense: string;
  one: string;
  many: string;
}

export async function runSequentially(keys: readonly string[], run: (key: string) => Promise<unknown>): Promise<BulkOutcome> {
  const succeeded: string[] = [];
  const failed: BulkFailure[] = [];
  for (const key of keys) {
    try {
      await run(key);
      succeeded.push(key);
    } catch (failure: unknown) {
      failed.push({ key, message: describeError(failure) });
    }
  }
  return { succeeded, failed };
}

export function countOf(count: number, one: string, many: string): string {
  return `${count} ${count === 1 ? one : many}`;
}

export function describeBulkOutcome(outcome: BulkOutcome, wording: BulkWording): ActionNotice {
  const total = outcome.succeeded.length + outcome.failed.length;
  if (outcome.failed.length === 0) {
    return { tone: 'ok', message: `${wording.pastTense} ${countOf(total, wording.one, wording.many)}. Recorded in the journal.` };
  }
  const failures = outcome.failed.map((failure) => `${failure.key}: ${failure.message}`).join('; ');
  return {
    tone: 'danger',
    message: `${wording.pastTense} ${outcome.succeeded.length} of ${countOf(total, wording.one, wording.many)}. Failed: ${failures}`,
  };
}
