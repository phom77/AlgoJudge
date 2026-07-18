import type { RunResponse } from '../../../core/api/generated/models/run-response';
import type { CodeRun, RunStatus } from './run.models';

export function mapRun(response: RunResponse): CodeRun {
  return {
    id: requiredText(response.id, 'id'),
    problemId: positiveInteger(response.problemId, 'problemId'),
    status: readStatus(response.status),
    stdout: nullableText(response.stdout),
    stderr: nullableText(response.stderr),
    executionTimeMs: metric(response.executionTimeMs),
    memoryUsedKb: metric(response.memoryUsedKb),
    createdAt: requiredTimestamp(response.createdAt, 'createdAt'),
    startedAt: optionalTimestamp(response.startedAt),
    finishedAt: optionalTimestamp(response.finishedAt),
  };
}

function readStatus(value: unknown): RunStatus {
  const byNumber: Readonly<Record<number, RunStatus>> = {
    0: 'Pending',
    1: 'Completed',
    2: 'Running',
    3: 'TimeLimitExceeded',
    4: 'MemoryLimitExceeded',
    5: 'CompileError',
    6: 'RuntimeError',
  };
  if (typeof value === 'string' && Object.values(byNumber).includes(value as RunStatus))
    return value as RunStatus;
  const status = byNumber[Number(value)];
  if (status === undefined) throw new Error('The run response contains an invalid status.');
  return status;
}

function requiredText(value: unknown, field: string): string {
  const text = typeof value === 'string' ? value.trim() : '';
  if (!text) throw new Error(`The run response is missing ${field}.`);
  return text;
}
function nullableText(value: unknown): string | null {
  return typeof value === 'string' ? value : null;
}
function positiveInteger(value: unknown, field: string): number {
  const number = Number(value);
  if (!Number.isInteger(number) || number <= 0)
    throw new Error(`The run response contains an invalid ${field}.`);
  return number;
}
function metric(value: unknown): number | null {
  if (value === null || value === undefined) return null;
  const number = Number(value);
  return Number.isInteger(number) && number >= 0 ? number : null;
}
function requiredTimestamp(value: unknown, field: string): string {
  const timestamp = optionalTimestamp(value);
  if (timestamp === null) throw new Error(`The run response contains an invalid ${field}.`);
  return timestamp;
}
function optionalTimestamp(value: unknown): string | null {
  return typeof value === 'string' && !Number.isNaN(Date.parse(value)) ? value : null;
}
