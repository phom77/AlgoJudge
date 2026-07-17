import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { SubmissionGateway } from './submission.gateway';
import type { Submission, SubmissionStatus } from './submission.models';
import { SubmissionPollingService } from './submission-polling.service';

describe('SubmissionPollingService', () => {
  const detail = vi.fn();
  let polling: SubmissionPollingService;

  beforeEach(() => {
    vi.useFakeTimers();
    detail.mockReset();
    TestBed.configureTestingModule({
      providers: [SubmissionPollingService, { provide: SubmissionGateway, useValue: { detail } }],
    });
    polling = TestBed.inject(SubmissionPollingService);
  });

  afterEach(() => vi.useRealTimers());

  it('uses bounded delays and stops after the first terminal verdict', async () => {
    detail
      .mockReturnValueOnce(of(submission('Running')))
      .mockReturnValueOnce(of(submission('Accepted')));
    const statuses: SubmissionStatus[] = [];

    polling.watch(submission('Pending')).subscribe((value) => statuses.push(value.status));
    expect(statuses).toEqual(['Pending']);

    await vi.advanceTimersByTimeAsync(1_000);
    expect(statuses).toEqual(['Pending', 'Running']);
    await vi.advanceTimersByTimeAsync(1_500);
    expect(statuses).toEqual(['Pending', 'Running', 'Accepted']);
    await vi.advanceTimersByTimeAsync(10_000);
    expect(detail).toHaveBeenCalledTimes(2);
  });
});

function submission(status: SubmissionStatus): Submission {
  return {
    id: '75b27e41-e942-42b1-89dc-4bc087f458c3',
    problemId: 7,
    language: 'cpp17',
    status,
    executionTimeMs: status === 'Accepted' ? 12 : null,
    memoryUsedKb: status === 'Accepted' ? 2048 : null,
    createdAt: '2026-07-17T00:00:00Z',
    startedAt: null,
    finishedAt: null,
  };
}
