import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { SubmissionDetailStore, isSubmissionIdentifier } from './submission-detail.store';
import { SubmissionGateway } from './submission.gateway';
import type { Submission } from './submission.models';
import { SubmissionPollingService } from './submission-polling.service';

describe('submission detail identifier validation', () => {
  it('accepts canonical UUIDs regardless of their version', () => {
    expect(isSubmissionIdentifier('0198f26a-9b10-7b67-8e0a-2b3c4d5e6f70')).toBe(true);
    expect(isSubmissionIdentifier('2d74e0cf-8d87-4c18-b9e8-8399a70a928d')).toBe(true);
  });

  it('rejects malformed submission identifiers', () => {
    expect(isSubmissionIdentifier('not-a-guid')).toBe(false);
    expect(isSubmissionIdentifier('2d74e0cf-8d87-4c18-b9e8')).toBe(false);
  });
});

describe('SubmissionDetailStore', () => {
  const detail = vi.fn();
  const content = vi.fn();
  const watch = vi.fn();

  beforeEach(() => {
    detail.mockReset();
    content.mockReset();
    watch.mockReset();
    TestBed.configureTestingModule({
      providers: [
        SubmissionDetailStore,
        { provide: SubmissionGateway, useValue: { detail, content } },
        { provide: SubmissionPollingService, useValue: { watch } },
      ],
    });
  });

  it('refreshes owner content after a pending submission becomes Compile Error', () => {
    detail.mockReturnValue(of(submission('Pending')));
    watch.mockReturnValue(of(submission('CompileError')));
    content
      .mockReturnValueOnce(of({ sourceCode: 'COMPILE_ERROR', compileMessage: null }))
      .mockReturnValueOnce(
        of({ sourceCode: 'COMPILE_ERROR', compileMessage: 'submission.cpp: error' }),
      );
    const store = TestBed.inject(SubmissionDetailStore);

    store.connect(of('75b27e41-e942-42b1-89dc-4bc087f458c3'));

    expect(content).toHaveBeenCalledTimes(2);
    expect(store.submission()?.status).toBe('CompileError');
    expect(store.content()?.compileMessage).toContain('submission.cpp');
  });
});

function submission(status: Submission['status']): Submission {
  return {
    id: '75b27e41-e942-42b1-89dc-4bc087f458c3',
    problemId: 7,
    problemTitle: 'Two Sum',
    problemSlug: 'two-sum',
    systemTestSuiteVersion: 1,
    language: 'cpp17',
    status,
    executionTimeMs: null,
    memoryUsedKb: null,
    createdAt: '2026-07-17T00:00:00Z',
    startedAt: null,
    finishedAt: null,
  };
}
