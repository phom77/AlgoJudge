import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, map, of, switchMap, tap } from 'rxjs';
import type { Observable } from 'rxjs';

import type { ProblemDraftResponse } from '../../../core/api/admin-generated/models/problem-draft-response';
import type { ApiProblem } from '../../../core/error/api-problem';
import { createUnknownApiProblem, isApiProblem } from '../../../core/error/api-problem';
import {
  INITIAL_ADMIN_PROBLEM_QUERY,
  type AdminProblemPage,
  type AdminProblemQuery,
} from './problem-management.models';
import { ProblemManagementGateway } from './problem-management.gateway';

interface ManagementState {
  readonly query: AdminProblemQuery;
  readonly page: AdminProblemPage | null;
  readonly loading: boolean;
  readonly actionProblemId: number | null;
  readonly problem: ApiProblem | null;
}

@Injectable()
export class ProblemManagementStore {
  private readonly gateway = inject(ProblemManagementGateway);
  private readonly state = signal<ManagementState>({
    query: INITIAL_ADMIN_PROBLEM_QUERY,
    page: null,
    loading: false,
    actionProblemId: null,
    problem: null,
  });

  readonly query = computed(() => this.state().query);
  readonly page = computed(() => this.state().page);
  readonly items = computed(() => this.state().page?.items ?? []);
  readonly totalCount = computed(() => this.state().page?.totalCount ?? 0);
  readonly loading = computed(() => this.state().loading);
  readonly actionProblemId = computed(() => this.state().actionProblemId);
  readonly problem = computed(() => this.state().problem);

  load(query = this.query()): Observable<void> {
    this.state.update((state) => ({ ...state, query, loading: true, problem: null }));
    return this.gateway.list(query).pipe(
      tap((response) =>
        this.state.update((state) => ({
          ...state,
          page: {
            items: response.items ?? [],
            totalCount: Number(response.totalCount ?? 0),
          },
        })),
      ),
      map(() => undefined),
      catchError((error: unknown) => {
        this.setProblem(error);
        return of(undefined);
      }),
      finalize(() => this.state.update((state) => ({ ...state, loading: false }))),
    );
  }

  createRevision(problemId: number): Observable<ProblemDraftResponse | null> {
    return this.run(problemId, this.gateway.createRevision(problemId));
  }

  archive(problemId: number): Observable<boolean> {
    return this.run(problemId, this.gateway.archive(problemId).pipe(map(() => true))).pipe(
      switchMap((completed) =>
        completed === null ? of(false) : this.load().pipe(map(() => true)),
      ),
    );
  }

  restore(problemId: number): Observable<boolean> {
    return this.run(problemId, this.gateway.restore(problemId).pipe(map(() => true))).pipe(
      switchMap((completed) =>
        completed === null ? of(false) : this.load().pipe(map(() => true)),
      ),
    );
  }

  private run<T>(problemId: number, source: Observable<T>): Observable<T | null> {
    this.state.update((state) => ({ ...state, actionProblemId: problemId, problem: null }));
    return source.pipe(
      catchError((error: unknown) => {
        this.setProblem(error);
        return of(null);
      }),
      finalize(() => this.state.update((state) => ({ ...state, actionProblemId: null }))),
    );
  }

  private setProblem(error: unknown): void {
    this.state.update((state) => ({
      ...state,
      problem: isApiProblem(error) ? error : createUnknownApiProblem(),
    }));
  }
}
