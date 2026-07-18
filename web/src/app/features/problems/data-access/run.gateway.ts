import { inject, Injectable } from '@angular/core';
import { catchError, map, switchMap, throwError } from 'rxjs';
import type { Observable } from 'rxjs';

import { AntiforgeryService } from '../../../core/api/antiforgery.service';
import { AlgoJudgeApi } from '../../../core/api/generated/algo-judge-api';
import { apiProblemsSlugRunsPost$Json } from '../../../core/api/generated/fn/problem-runs/api-problems-slug-runs-post-json';
import { apiRunsIdGet$Json } from '../../../core/api/generated/fn/runs/api-runs-id-get-json';
import { mapProblemDetails } from '../../../core/api/problem-details.mapper';
import { mapRun } from './run.mapper';
import type { CodeRun, RunInput } from './run.models';

@Injectable({ providedIn: 'root' })
export class RunGateway {
  private readonly api = inject(AlgoJudgeApi);
  private readonly antiforgery = inject(AntiforgeryService);

  create(slug: string, sourceCode: string, runInput: RunInput): Observable<CodeRun> {
    return this.antiforgery.ensureToken().pipe(
      switchMap(() =>
        this.api.invoke(apiProblemsSlugRunsPost$Json, {
          slug,
          body: { sourceCode, language: 'cpp17', ...runInput },
        }),
      ),
      map(mapRun),
      catchError((error: unknown) => {
        const problem = mapProblemDetails(error);
        if (problem.code === 'csrf') this.antiforgery.invalidate();
        return throwError(() => problem);
      }),
    );
  }

  detail(id: string): Observable<CodeRun> {
    return this.api.invoke(apiRunsIdGet$Json, { id }).pipe(
      map(mapRun),
      catchError((error: unknown) => throwError(() => mapProblemDetails(error))),
    );
  }
}
