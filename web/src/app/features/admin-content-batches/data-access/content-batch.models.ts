import type { ContentBatchItemResponse } from '../../../core/api/admin-generated/models/content-batch-item-response';
import type { ApiProblem } from '../../../core/error/api-problem';

export type ContentBatchFilter =
  'all' | 'pending' | 'generating' | 'ready' | 'failed' | 'published' | 'skipped';

export interface ContentBatchItemQuery {
  readonly search: string;
  readonly status: ContentBatchFilter;
}

export const INITIAL_CONTENT_BATCH_ITEM_QUERY: ContentBatchItemQuery = {
  search: '',
  status: 'all',
};

export function batchStatusLabel(value: unknown): string {
  return enumLabel(value, {
    0: 'Created',
    1: 'Validating',
    2: 'Generating',
    3: 'Ready for review',
    4: 'Publishing',
    5: 'Completed',
  });
}

export function itemStatusLabel(value: unknown): string {
  return enumLabel(value, {
    0: 'Pending',
    1: 'Generating',
    2: 'Ready',
    3: 'Published',
    4: 'Failed',
    5: 'Retrying',
    6: 'Skipped',
  });
}

export function importActionLabel(value: unknown): string {
  return enumLabel(value, {
    0: 'Create',
    1: 'Update draft',
    2: 'New revision',
    3: 'Skip',
  });
}

export function isBatchStatus(value: unknown, expected: string): boolean {
  return batchStatusLabel(value).toLowerCase() === expected.toLowerCase();
}

export function isItemStatus(value: unknown, expected: string): boolean {
  return itemStatusLabel(value).toLowerCase() === expected.toLowerCase();
}

export function isRetryableItem(item: ContentBatchItemResponse): boolean {
  return (
    isItemStatus(item.status, 'Failed') &&
    !['invalid_definition', 'invalid_path', 'duplicate_slug'].includes(
      item.safeFailureCategory ?? '',
    )
  );
}

export function isActiveBatchStatus(value: unknown): boolean {
  const status = batchStatusLabel(value);
  return status === 'Validating' || status === 'Generating' || status === 'Publishing';
}

export function matchesItemQuery(
  item: ContentBatchItemResponse,
  query: ContentBatchItemQuery,
): boolean {
  const search = query.search.trim().toLowerCase();
  const matchesSearch =
    search.length === 0 ||
    (item.slug ?? '').toLowerCase().includes(search) ||
    (item.title ?? '').toLowerCase().includes(search);
  const matchesStatus =
    query.status === 'all' || itemStatusLabel(item.status).toLowerCase() === query.status;
  return matchesSearch && matchesStatus;
}

export function batchProblemMessage(problem: ApiProblem): string {
  switch (problem.code) {
    case 'authentication':
      return 'Your Admin session expired. Sign in again before continuing.';
    case 'forbidden':
      return 'Your account is not allowed to administer content batches.';
    case 'conflict':
      return problem.detail ?? 'The batch changed. Refresh it before retrying this action.';
    case 'network':
    case 'internal':
      return 'The API or content worker may be unavailable. Progress was preserved; try again later.';
    default:
      return problem.detail ?? problem.title;
  }
}

function enumLabel(value: unknown, labels: Readonly<Record<number, string>>): string {
  if (typeof value === 'string') {
    const normalized = value
      .trim()
      .replaceAll(/[\s_-]/g, '')
      .toLowerCase();
    const match = Object.values(labels).find(
      (label) => label.replaceAll(/[\s_-]/g, '').toLowerCase() === normalized,
    );
    if (match) return match;
  }
  return labels[Number(value)] ?? 'Unknown';
}
