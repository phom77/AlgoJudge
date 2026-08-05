import type { PagedResponseOfSubmissionResponse } from '../../../core/api/generated/models/paged-response-of-submission-response';
import type { SubmissionResponse } from '../../../core/api/generated/models/submission-response';
import type { SubmissionContentResponse } from '../../../core/api/generated/models/submission-content-response';
import type {
  Submission,
  SubmissionContent,
  SubmissionPage,
  SubmissionStatus,
} from './submission.models';

export function mapSubmission(response: SubmissionResponse): Submission {
  return {
    id: readRequiredText(response.id, 'id'),
    problemId: readPositiveInteger(response.problemId, 'problemId'),
    problemTitle: readRequiredText(response.problemTitle, 'problemTitle'),
    problemSlug: readRequiredText(response.problemSlug, 'problemSlug'),
    systemTestSuiteVersion: readOptionalPositiveInteger(response.systemTestSuiteVersion),
    language: readLanguage(response.language),
    status: readStatus(response.status),
    executionTimeMs: readOptionalMetric(response.executionTimeMs),
    memoryUsedKb: readOptionalMetric(response.memoryUsedKb),
    createdAt: readRequiredTimestamp(response.createdAt, 'createdAt'),
    startedAt: readOptionalTimestamp(response.startedAt),
    finishedAt: readOptionalTimestamp(response.finishedAt),
  };
}

export function mapSubmissionContent(response: SubmissionContentResponse): SubmissionContent {
  return {
    sourceCode: readRequiredText(response.sourceCode, 'sourceCode', false),
    compileMessage: readOptionalText(response.compileMessage),
  };
}

export function mapSubmissionPage(response: PagedResponseOfSubmissionResponse): SubmissionPage {
  return {
    items: (response.items ?? []).map(mapSubmission),
    pageNumber: readPageValue(response.pageNumber, 1),
    pageSize: readPageValue(response.pageSize, 20),
    totalCount: readPageValue(response.totalCount, 0),
    totalPages: readPageValue(response.totalPages, 0),
  };
}

function readStatus(value: unknown): SubmissionStatus {
  const byNumber: Readonly<Record<number, SubmissionStatus>> = {
    1: 'Pending',
    2: 'Running',
    3: 'Accepted',
    4: 'WrongAnswer',
    5: 'TimeLimitExceeded',
    6: 'MemoryLimitExceeded',
    7: 'CompileError',
    8: 'RuntimeError',
  };
  if (typeof value === 'string' && Object.values(byNumber).includes(value as SubmissionStatus)) {
    return value as SubmissionStatus;
  }
  const status = byNumber[Number(value)];
  if (status === undefined) throw new Error('The submission response contains an invalid status.');
  return status;
}

function readLanguage(value: unknown): 'cpp17' {
  if (value !== 'cpp17') throw new Error('The submission response contains an invalid language.');
  return value;
}

function readRequiredText(value: unknown, field: string, trim = true): string {
  const text = typeof value === 'string' ? value : '';
  if (text.trim().length === 0) throw new Error(`The submission response is missing ${field}.`);
  return trim ? text.trim() : text;
}

function readOptionalText(value: unknown): string | null {
  return typeof value === 'string' && value.trim().length > 0 ? value : null;
}

function readPositiveInteger(value: unknown, field: string): number {
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed <= 0) {
    throw new Error(`The submission response contains an invalid ${field}.`);
  }
  return parsed;
}

function readOptionalMetric(value: unknown): number | null {
  if (value === undefined || value === null) return null;
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= 0 ? parsed : null;
}

function readOptionalPositiveInteger(value: unknown): number | null {
  if (value === undefined || value === null) return null;
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function readRequiredTimestamp(value: unknown, field: string): string {
  const timestamp = readOptionalTimestamp(value);
  if (timestamp === null) throw new Error(`The submission response contains an invalid ${field}.`);
  return timestamp;
}

function readOptionalTimestamp(value: unknown): string | null {
  return typeof value === 'string' && !Number.isNaN(Date.parse(value)) ? value : null;
}

function readPageValue(value: unknown, fallback: number): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= 0 ? parsed : fallback;
}
