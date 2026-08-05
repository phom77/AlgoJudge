import type { ParamMap, Params } from '@angular/router';

import {
  DEFAULT_SUBMISSION_QUERY,
  SUBMISSION_STATUSES,
  type SubmissionHistoryQuery,
  type SubmissionStatus,
} from './submission.models';

export function readSubmissionQuery(params: ParamMap): SubmissionHistoryQuery {
  return {
    problemId: readPositiveInteger(params.get('problemId')),
    problemSearch: readSearch(params.get('problemSearch')),
    status: readStatus(params.get('status')),
    pageNumber: readInteger(
      params.get('page'),
      DEFAULT_SUBMISSION_QUERY.pageNumber,
      1,
      2_147_483_647,
    ),
    pageSize: readInteger(params.get('pageSize'), DEFAULT_SUBMISSION_QUERY.pageSize, 1, 100),
  };
}

export function writeSubmissionQuery(query: SubmissionHistoryQuery): Params {
  return {
    problemId: query.problemId,
    problemSearch: query.problemSearch || null,
    status: query.status,
    page: query.pageNumber === DEFAULT_SUBMISSION_QUERY.pageNumber ? null : query.pageNumber,
    pageSize: query.pageSize === DEFAULT_SUBMISSION_QUERY.pageSize ? null : query.pageSize,
  };
}

export function submissionQueryKey(query: SubmissionHistoryQuery): string {
  return [
    query.problemId ?? '',
    query.problemSearch,
    query.status ?? '',
    query.pageNumber,
    query.pageSize,
  ].join('|');
}

function readSearch(value: string | null): string {
  return (value ?? '').trim().slice(0, 100);
}

function readStatus(value: string | null): SubmissionStatus | null {
  return SUBMISSION_STATUSES.find((status) => status === value) ?? null;
}

function readPositiveInteger(value: string | null): number | null {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : null;
}

function readInteger(
  value: string | null,
  fallback: number,
  minimum: number,
  maximum: number,
): number {
  const parsed = Number(value);
  return Number.isInteger(parsed) && parsed >= minimum && parsed <= maximum ? parsed : fallback;
}
