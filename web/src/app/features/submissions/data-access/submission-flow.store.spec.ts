import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';

import { AuthStore } from '../../../core/auth/auth.store';
import { SubmissionFlowStore } from './submission-flow.store';
import { SubmissionGateway } from './submission.gateway';
import type { Submission } from './submission.models';
import { SubmissionPollingService } from './submission-polling.service';

describe('SubmissionFlowStore', () => {
  const isAuthenticated = signal(true);
  const create = vi.fn();
  const watch = vi.fn();
  let store: SubmissionFlowStore;

  beforeEach(() => {
    create.mockReset();
    watch.mockReset();
    isAuthenticated.set(true);
    TestBed.configureTestingModule({
      providers: [
        SubmissionFlowStore,
        { provide: AuthStore, useValue: { isAuthenticated } },
        { provide: SubmissionGateway, useValue: { create } },
        { provide: SubmissionPollingService, useValue: { watch } },
      ],
    });
    store = TestBed.inject(SubmissionFlowStore);
  });

  it('prevents duplicate creation while a request is active', () => {
    const created = new Subject<Submission>();
    create.mockReturnValue(created);
    watch.mockImplementation((value: Submission) => of(value));

    store.submit(7, 'int main() {}').subscribe();
    store.submit(7, 'int main() {}').subscribe();

    expect(create).toHaveBeenCalledOnce();
    expect(store.submitting()).toBe(true);
    created.next(submission('Accepted'));
    created.complete();
    expect(store.submitting()).toBe(false);
    expect(store.submission()?.status).toBe('Accepted');
  });

  it('rejects UTF-8 source larger than the backend limit without an API call', () => {
    store.submit(7, '😀'.repeat(20_000)).subscribe();

    expect(create).not.toHaveBeenCalled();
    expect(store.problem()?.code).toBe('validation');
    expect(store.problem()?.detail).toContain('65,536 UTF-8 bytes');
  });
});

function submission(status: Submission['status']): Submission {
  return {
    id: '75b27e41-e942-42b1-89dc-4bc087f458c3',
    problemId: 7,
    language: 'cpp17',
    status,
    executionTimeMs: 12,
    memoryUsedKb: 2048,
    createdAt: '2026-07-17T00:00:00Z',
    startedAt: null,
    finishedAt: null,
  };
}
