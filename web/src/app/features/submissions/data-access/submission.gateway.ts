import { inject, Injectable } from '@angular/core';
import { catchError, map, switchMap, throwError } from 'rxjs';
import type { Observable } from 'rxjs';

import { AntiforgeryService } from '../../../core/api/antiforgery.service';
import { AlgoJudgeApi } from '../../../core/api/generated/algo-judge-api';
import { apiSubmissionsGet$Json } from '../../../core/api/generated/fn/submissions/api-submissions-get-json';
import { apiSubmissionsIdGet$Json } from '../../../core/api/generated/fn/submissions/api-submissions-id-get-json';
import { apiSubmissionsPost$Json } from '../../../core/api/generated/fn/submissions/api-submissions-post-json';
import { mapProblemDetails } from '../../../core/api/problem-details.mapper';
import { mapSubmission, mapSubmissionPage } from './submission.mapper';
import type {
  Submission,
  SubmissionHistoryQuery,
  SubmissionPage,
  SubmissionStatus,
} from './submission.models';

@Injectable({ providedIn: 'root' })
export class SubmissionGateway {
  private readonly api = inject(AlgoJudgeApi);
  private readonly antiforgery = inject(AntiforgeryService);

  create(problemId: number, sourceCode: string): Observable<Submission> {
    return this.antiforgery.ensureToken().pipe(
      switchMap(() =>
        this.api.invoke(apiSubmissionsPost$Json, {
          body: { problemId, sourceCode, language: 'cpp17' },
        }),
      ),
      map(mapSubmission),
      catchError((error: unknown) => this.rethrowProblem(error, true)),
    );
  }

  detail(id: string): Observable<Submission> {
    return this.api.invoke(apiSubmissionsIdGet$Json, { id }).pipe(
      map(mapSubmission),
      catchError((error: unknown) => this.rethrowProblem(error)),
    );
  }

  history(query: SubmissionHistoryQuery): Observable<SubmissionPage> {
    return this.api
      .invoke(apiSubmissionsGet$Json, {
        ProblemId: query.problemId ?? undefined,
        Status: toApiStatus(query.status),
        PageNumber: query.pageNumber,
        PageSize: query.pageSize,
      })
      .pipe(
        map(mapSubmissionPage),
        catchError((error: unknown) => this.rethrowProblem(error)),
      );
  }

  private rethrowProblem(error: unknown, unsafe = false): Observable<never> {
    const problem = mapProblemDetails(error);
    if (unsafe && problem.code === 'csrf') this.antiforgery.invalidate();
    return throwError(() => problem);
  }
}

function toApiStatus(status: SubmissionStatus | null): number | undefined {
  if (status === null) return undefined;
  return {
    Pending: 1,
    Running: 2,
    Accepted: 3,
    WrongAnswer: 4,
    TimeLimitExceeded: 5,
    MemoryLimitExceeded: 6,
    CompileError: 7,
    RuntimeError: 8,
  }[status];
}
