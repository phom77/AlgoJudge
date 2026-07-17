import { inject, Injectable } from '@angular/core';
import { EMPTY, expand, of, switchMap, timer } from 'rxjs';
import type { Observable } from 'rxjs';

import { SubmissionGateway } from './submission.gateway';
import { isTerminalStatus, type Submission } from './submission.models';

const POLL_DELAYS_MS = [1_000, 1_500, 2_000, 3_000] as const;

@Injectable({ providedIn: 'root' })
export class SubmissionPollingService {
  private readonly gateway = inject(SubmissionGateway);

  watch(initial: Submission): Observable<Submission> {
    return of(initial).pipe(
      expand((submission, attempt) => {
        if (isTerminalStatus(submission.status)) return EMPTY;
        const delay = POLL_DELAYS_MS[Math.min(attempt, POLL_DELAYS_MS.length - 1)];
        return timer(delay).pipe(switchMap(() => this.gateway.detail(submission.id)));
      }),
    );
  }
}
