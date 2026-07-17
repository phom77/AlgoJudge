import { DestroyRef, computed, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, distinctUntilChanged, map, of, Subject, switchMap } from 'rxjs';
import type { Observable } from 'rxjs';

import type { ApiProblem } from '../../../core/error/api-problem';
import { createUnknownApiProblem, isApiProblem } from '../../../core/error/api-problem';
import { ProblemGateway } from './problem.gateway';
import type { ProblemDetail } from './problem.models';

interface WorkspaceState {
  readonly slug: string;
  readonly detail: ProblemDetail | null;
  readonly loading: boolean;
  readonly problem: ApiProblem | null;
}

@Injectable()
export class ProblemWorkspaceStore {
  private readonly destroyRef = inject(DestroyRef);
  private readonly gateway = inject(ProblemGateway);
  private readonly requests = new Subject<string>();
  private readonly state = signal<WorkspaceState>({
    slug: '',
    detail: null,
    loading: true,
    problem: null,
  });

  readonly detail = computed(() => this.state().detail);
  readonly loading = computed(() => this.state().loading);
  readonly problem = computed(() => this.state().problem);

  constructor() {
    this.requests
      .pipe(
        switchMap((slug) => {
          this.state.set({ slug, detail: null, loading: true, problem: null });
          return this.gateway.detail(slug).pipe(
            map((detail) => ({ slug, detail, problem: null })),
            catchError((error: unknown) =>
              of({ slug, detail: null, problem: asApiProblem(error) }),
            ),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => this.state.set({ ...result, loading: false }));
  }

  connect(slug$: Observable<string>): void {
    slug$
      .pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((slug) => this.requests.next(slug));
  }

  retry(): void {
    this.requests.next(this.state().slug);
  }
}

function asApiProblem(error: unknown): ApiProblem {
  return isApiProblem(error) ? error : createUnknownApiProblem();
}
