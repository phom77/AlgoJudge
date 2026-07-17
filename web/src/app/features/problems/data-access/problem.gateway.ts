import { inject, Injectable } from '@angular/core';
import { catchError, map, throwError } from 'rxjs';
import type { Observable } from 'rxjs';

import { mapProblemDetails } from '../../../core/api/problem-details.mapper';
import { AlgoJudgeApi } from '../../../core/api/generated/algo-judge-api';
import { apiProblemsGet$Json } from '../../../core/api/generated/fn/problems/api-problems-get-json';
import { apiProblemsSlugGet$Json } from '../../../core/api/generated/fn/problems/api-problems-slug-get-json';
import { mapProblemDetail, mapProblemPage } from './problem.mapper';
import type {
  ProblemCatalogueQuery,
  ProblemDetail,
  ProblemDifficulty,
  ProblemPage,
} from './problem.models';

@Injectable({ providedIn: 'root' })
export class ProblemGateway {
  private readonly api = inject(AlgoJudgeApi);

  list(query: ProblemCatalogueQuery): Observable<ProblemPage> {
    return this.api
      .invoke(apiProblemsGet$Json, {
        Search: query.search || undefined,
        Difficulty: toApiDifficulty(query.difficulty),
        Tags: query.tags.length > 0 ? [...query.tags] : undefined,
        Solved: query.solved ?? undefined,
        PageNumber: query.pageNumber,
        PageSize: query.pageSize,
      })
      .pipe(
        map(mapProblemPage),
        catchError((error: unknown) => this.rethrowProblem(error)),
      );
  }

  detail(slug: string): Observable<ProblemDetail> {
    return this.api.invoke(apiProblemsSlugGet$Json, { slug }).pipe(
      map(mapProblemDetail),
      catchError((error: unknown) => this.rethrowProblem(error)),
    );
  }

  private rethrowProblem(error: unknown): Observable<never> {
    return throwError(() => mapProblemDetails(error));
  }
}

function toApiDifficulty(difficulty: ProblemDifficulty | null): number | undefined {
  switch (difficulty) {
    case 'Easy':
      return 1;
    case 'Medium':
      return 2;
    case 'Hard':
      return 3;
    default:
      return undefined;
  }
}
