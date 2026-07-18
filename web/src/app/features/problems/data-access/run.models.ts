export const RUN_STATUSES = [
  'Pending',
  'Completed',
  'Running',
  'TimeLimitExceeded',
  'MemoryLimitExceeded',
  'CompileError',
  'RuntimeError',
] as const;

export type RunStatus = (typeof RUN_STATUSES)[number];

export interface CodeRun {
  readonly id: string;
  readonly problemId: number;
  readonly status: RunStatus;
  readonly stdout: string | null;
  readonly stderr: string | null;
  readonly executionTimeMs: number | null;
  readonly memoryUsedKb: number | null;
  readonly createdAt: string;
  readonly startedAt: string | null;
  readonly finishedAt: string | null;
}

export interface RunInput {
  readonly input?: string;
  readonly arguments?: Readonly<Record<string, unknown>>;
}

export function isTerminalRunStatus(status: RunStatus): boolean {
  return status !== 'Pending' && status !== 'Running';
}
