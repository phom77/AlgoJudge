import { inject, Injectable } from '@angular/core';
import { EMPTY, expand, of, switchMap, timer } from 'rxjs';
import type { Observable } from 'rxjs';
import { RunGateway } from './run.gateway';
import { isTerminalRunStatus, type CodeRun } from './run.models';

const POLL_DELAYS_MS = [750, 1_000, 1_500, 2_000] as const;

@Injectable({ providedIn: 'root' })
export class RunPollingService {
  private readonly gateway = inject(RunGateway);
  watch(initial: CodeRun): Observable<CodeRun> {
    return of(initial).pipe(
      expand((run, attempt) => {
        if (isTerminalRunStatus(run.status)) return EMPTY;
        const delay = POLL_DELAYS_MS[Math.min(attempt, POLL_DELAYS_MS.length - 1)];
        return timer(delay).pipe(switchMap(() => this.gateway.detail(run.id)));
      }),
    );
  }
}
