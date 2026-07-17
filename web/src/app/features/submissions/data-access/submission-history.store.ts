import { DestroyRef, computed, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, distinctUntilChanged, map, of, Subject, switchMap } from 'rxjs';
import type { Observable } from 'rxjs';

import type { ApiProblem } from '../../../core/error/api-problem';
import { createUnknownApiProblem, isApiProblem } from '../../../core/error/api-problem';
import { SubmissionGateway } from './submission.gateway';
import { submissionQueryKey } from './submission-history-query';
import { DEFAULT_SUBMISSION_QUERY } from './submission.models';
import type { SubmissionHistoryQuery, SubmissionPage } from './submission.models';

interface HistoryState {
  readonly query: SubmissionHistoryQuery;
  readonly page: SubmissionPage | null;
  readonly loading: boolean;
  readonly problem: ApiProblem | null;
}

@Injectable()
export class SubmissionHistoryStore {
  private readonly destroyRef = inject(DestroyRef);
  private readonly gateway = inject(SubmissionGateway);
  private readonly requests = new Subject<SubmissionHistoryQuery>();
  private readonly state = signal<HistoryState>({
    query: DEFAULT_SUBMISSION_QUERY,
    page: null,
    loading: true,
    problem: null,
  });

  readonly query = computed(() => this.state().query);
  readonly page = computed(() => this.state().page);
  readonly items = computed(() => this.state().page?.items ?? []);
  readonly loading = computed(() => this.state().loading);
  readonly problem = computed(() => this.state().problem);

  constructor() {
    this.requests
      .pipe(
        switchMap((query) => {
          this.state.update((state) => ({ ...state, query, loading: true, problem: null }));
          return this.gateway.history(query).pipe(
            map((page) => ({ query, page, problem: null })),
            catchError((error: unknown) => of({ query, page: null, problem: asApiProblem(error) })),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => this.state.set({ ...result, loading: false }));
  }

  connect(query$: Observable<SubmissionHistoryQuery>): void {
    query$
      .pipe(
        distinctUntilChanged(
          (left, right) => submissionQueryKey(left) === submissionQueryKey(right),
        ),
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
