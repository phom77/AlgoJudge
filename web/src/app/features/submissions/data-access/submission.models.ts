export const SUBMISSION_STATUSES = [
  'Pending',
  'Running',
  'Accepted',
  'WrongAnswer',
  'TimeLimitExceeded',
  'MemoryLimitExceeded',
  'CompileError',
  'RuntimeError',
] as const;

export type SubmissionStatus = (typeof SUBMISSION_STATUSES)[number];

export interface Submission {
  readonly id: string;
  readonly problemId: number;
  readonly language: 'cpp17';
  readonly status: SubmissionStatus;
  readonly executionTimeMs: number | null;
  readonly memoryUsedKb: number | null;
  readonly createdAt: string;
  readonly startedAt: string | null;
  readonly finishedAt: string | null;
}

export interface SubmissionPage {
  readonly items: readonly Submission[];
  readonly pageNumber: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
}

export interface SubmissionHistoryQuery {
  readonly problemId: number | null;
  readonly status: SubmissionStatus | null;
  readonly pageNumber: number;
  readonly pageSize: number;
}

export const DEFAULT_SUBMISSION_QUERY: SubmissionHistoryQuery = {
  problemId: null,
  status: null,
  pageNumber: 1,
  pageSize: 20,
};

export function isTerminalStatus(status: SubmissionStatus): boolean {
  return status !== 'Pending' && status !== 'Running';
}

export function submissionStatusLabel(status: SubmissionStatus): string {
  return {
    Pending: 'Pending',
    Running: 'Running',
    Accepted: 'Accepted',
    WrongAnswer: 'Wrong Answer',
    TimeLimitExceeded: 'Time Limit Exceeded',
    MemoryLimitExceeded: 'Memory Limit Exceeded',
    CompileError: 'Compile Error',
    RuntimeError: 'Runtime Error',
  }[status];
}
