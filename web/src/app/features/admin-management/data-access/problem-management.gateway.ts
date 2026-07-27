import { inject, Injectable } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import type { Observable } from 'rxjs';

import { AntiforgeryService } from '../../../core/api/antiforgery.service';
import { AlgoJudgeAdminApi } from '../../../core/api/admin-generated/algo-judge-admin-api';
import { apiInternalAdminProblemsGet$Json } from '../../../core/api/admin-generated/fn/problem-management/api-internal-admin-problems-get-json';
import { apiInternalAdminProblemsProblemIdArchivePost } from '../../../core/api/admin-generated/fn/problem-management/api-internal-admin-problems-problem-id-archive-post';
import { apiInternalAdminProblemsProblemIdRestorePost } from '../../../core/api/admin-generated/fn/problem-management/api-internal-admin-problems-problem-id-restore-post';
import { apiInternalAdminProblemsProblemIdRevisionsPost$Json } from '../../../core/api/admin-generated/fn/problem-management/api-internal-admin-problems-problem-id-revisions-post-json';
import type { PagedResponseOfAdminProblemListItemResponse } from '../../../core/api/admin-generated/models/paged-response-of-admin-problem-list-item-response';
import type { ProblemDraftResponse } from '../../../core/api/admin-generated/models/problem-draft-response';
import { mapProblemDetails } from '../../../core/api/problem-details.mapper';
import type { AdminProblemQuery } from './problem-management.models';
import { toProblemStatus } from './problem-management.models';

@Injectable({ providedIn: 'root' })
export class ProblemManagementGateway {
  private readonly api = inject(AlgoJudgeAdminApi);
  private readonly antiforgery = inject(AntiforgeryService);

  list(query: AdminProblemQuery): Observable<PagedResponseOfAdminProblemListItemResponse> {
    return this.wrap(
      this.api.invoke(apiInternalAdminProblemsGet$Json, {
        PageNumber: 1,
        PageSize: 100,
        Search: query.search.trim() || undefined,
        Status: toProblemStatus(query.status),
      }),
    );
  }

  createRevision(problemId: number): Observable<ProblemDraftResponse> {
    return this.unsafe(
      this.api.invoke(apiInternalAdminProblemsProblemIdRevisionsPost$Json, { problemId }),
    );
  }

  archive(problemId: number): Observable<void> {
    return this.unsafe(
      this.api.invoke(apiInternalAdminProblemsProblemIdArchivePost, { problemId }),
    );
  }

  restore(problemId: number): Observable<void> {
    return this.unsafe(
      this.api.invoke(apiInternalAdminProblemsProblemIdRestorePost, { problemId }),
    );
  }

  private unsafe<T>(source: Observable<T>): Observable<T> {
    return this.antiforgery.ensureToken().pipe(
      switchMap(() => source),
      catchError((error: unknown) => this.rethrowProblem(error, true)),
    );
  }

  private wrap<T>(source: Observable<T>): Observable<T> {
    return source.pipe(catchError((error: unknown) => this.rethrowProblem(error)));
  }

  private rethrowProblem(error: unknown, unsafe = false): Observable<never> {
    const problem = mapProblemDetails(error);
    if (unsafe && problem.code === 'csrf') this.antiforgery.invalidate();
    return throwError(() => problem);
  }
}
