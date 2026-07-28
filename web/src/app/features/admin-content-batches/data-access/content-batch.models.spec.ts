import type { ApiProblem } from '../../../core/error/api-problem';
import {
  batchProblemMessage,
  batchStatusLabel,
  importActionLabel,
  isActiveBatchStatus,
  isRetryableItem,
  itemStatusLabel,
  matchesItemQuery,
} from './content-batch.models';

describe('content batch view-model helpers', () => {
  it('maps numeric and string lifecycle values', () => {
    expect(batchStatusLabel(3)).toBe('Ready for review');
    expect(batchStatusLabel('ReadyForReview')).toBe('Ready for review');
    expect(itemStatusLabel(5)).toBe('Retrying');
    expect(itemStatusLabel('Published')).toBe('Published');
    expect(importActionLabel(2)).toBe('New revision');
    expect(isActiveBatchStatus('Generating')).toBe(true);
    expect(isActiveBatchStatus('Completed')).toBe(false);
    expect(isRetryableItem({ status: 4, safeFailureCategory: 'compile_error' })).toBe(true);
    expect(isRetryableItem({ status: 4, safeFailureCategory: 'invalid_path' })).toBe(false);
  });

  it('searches slug and title and filters status without private fields', () => {
    const item = {
      id: 'item-1',
      slug: 'maximum-subarray',
      title: 'Maximum Subarray',
      status: 4,
    };

    expect(matchesItemQuery(item, { search: 'maximum', status: 'failed' })).toBe(true);
    expect(matchesItemQuery(item, { search: 'subarray', status: 'ready' })).toBe(false);
    expect(Object.keys(item)).not.toContain('definition');
    expect(Object.keys(item)).not.toContain('source');
  });

  it('provides explicit authorization, conflict, and worker-unavailable UX', () => {
    expect(batchProblemMessage(problem('authentication'))).toContain('session expired');
    expect(batchProblemMessage(problem('forbidden'))).toContain('not allowed');
    expect(batchProblemMessage(problem('conflict'))).toContain('changed');
    expect(batchProblemMessage(problem('network'))).toContain('worker may be unavailable');
  });
});

function problem(code: ApiProblem['code']): ApiProblem {
  return {
    status: code === 'forbidden' ? 403 : 0,
    code,
    title: 'Problem',
    detail: null,
    type: null,
    instance: null,
    traceId: null,
    validationErrors: {},
    retryAfterSeconds: null,
  };
}
