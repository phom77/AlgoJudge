import { DestroyRef, computed, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, distinctUntilChanged, map, of, Subject, switchMap } from 'rxjs';
import type { Observable } from 'rxjs';

import type { ApiProblem } from '../../../core/error/api-problem';
import { createUnknownApiProblem, isApiProblem } from '../../../core/error/api-problem';
import { ProblemGateway } from './problem.gateway';
import { problemQueryKey } from './problem-query';
import { DEFAULT_PROBLEM_QUERY } from './problem.models';
import type { ProblemCatalogueQuery, ProblemPage } from './problem.models';

interface CatalogueState {
  readonly query: ProblemCatalogueQuery;
  readonly page: ProblemPage | null;
  readonly loading: boolean;
  readonly problem: ApiProblem | null;
}

@Injectable()
export class ProblemCatalogueStore {
  private readonly destroyRef = inject(DestroyRef);
  private readonly gateway = inject(ProblemGateway);
  private readonly requests = new Subject<ProblemCatalogueQuery>();
  private readonly state = signal<CatalogueState>({
    query: DEFAULT_PROBLEM_QUERY,
    page: null,
    loading: true,
    problem: null,
  });

  readonly query = computed(() => this.state().query);
  readonly page = computed(() => this.state().page);
  readonly items = computed(() => this.state().page?.items ?? []);
  readonly loading = computed(() => this.state().loading);
  readonly problem = computed(() => this.state().problem);
  readonly availableTags = computed(() => {
    const tags = this.items().flatMap((problem) => problem.tags);
    return [...new Map(tags.map((tag) => [tag.slug, tag])).values()].sort((a, b) =>
      a.name.localeCompare(b.name),
    );
  });

  constructor() {
    this.requests
      .pipe(
        switchMap((query) => {
          this.state.update((state) => ({ ...state, query, loading: true, problem: null }));
          return this.gateway.list(query).pipe(
            map((page) => ({ query, page, problem: null })),
            catchError((error: unknown) => of({ query, page: null, problem: asApiProblem(error) })),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => {
        this.state.set({ ...result, loading: false });
      });
  }

  connect(query$: Observable<ProblemCatalogueQuery>): void {
    query$
      .pipe(
        distinctUntilChanged((left, right) => problemQueryKey(left) === problemQueryKey(right)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((query) => this.requests.next(query));
  }

  retry(): void {
    this.requests.next(this.query());
  }
}

function asApiProblem(error: unknown): ApiProblem {
  return isApiProblem(error) ? error : createUnknownApiProblem();
}
