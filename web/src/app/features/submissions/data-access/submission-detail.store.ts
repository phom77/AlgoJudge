import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, distinctUntilChanged, map, of, Subject, switchMap } from 'rxjs';
import type { Observable } from 'rxjs';

import type { ApiProblem } from '../../../core/error/api-problem';
import { createUnknownApiProblem, isApiProblem } from '../../../core/error/api-problem';
import { SubmissionGateway } from './submission.gateway';
import type { Submission } from './submission.models';
import { SubmissionPollingService } from './submission-polling.service';

interface DetailState {
  readonly id: string;
  readonly submission: Submission | null;
  readonly loading: boolean;
  readonly problem: ApiProblem | null;
}

@Injectable()
export class SubmissionDetailStore {
  private readonly destroyRef = inject(DestroyRef);
  private readonly gateway = inject(SubmissionGateway);
  private readonly polling = inject(SubmissionPollingService);
  private readonly requests = new Subject<string>();
  private readonly state = signal<DetailState>({
    id: '',
    submission: null,
    loading: true,
    problem: null,
  });

  readonly submission = computed(() => this.state().submission);
  readonly loading = computed(() => this.state().loading);
  readonly problem = computed(() => this.state().problem);

  constructor() {
    this.requests
      .pipe(
        switchMap((id) => this.load(id)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => this.state.set(result));
  }

  connect(id$: Observable<string>): void {
    id$
      .pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((id) => this.requests.next(id));
  }

  retry(): void {
    this.requests.next(this.state().id);
  }

  private load(id: string): Observable<DetailState> {
    this.state.set({ id, submission: null, loading: true, problem: null });
    if (!isUuid(id)) return of(invalidIdState(id));
    return this.gateway.detail(id).pipe(
      switchMap((submission) => this.polling.watch(submission)),
      map((submission) => ({ id, submission, loading: false, problem: null })),
      catchError((error: unknown) =>
        of({ id, submission: null, loading: false, problem: asApiProblem(error) }),
      ),
    );
  }
}

function invalidIdState(id: string): DetailState {
  return {
    id,
    submission: null,
    loading: false,
    problem: {
      status: 400,
      code: 'validation',
      title: 'Invalid submission identifier.',
      detail: null,
      type: null,
      instance: null,
      traceId: null,
      validationErrors: {},
      retryAfterSeconds: null,
    },
  };
}

function isUuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
}

function asApiProblem(error: unknown): ApiProblem {
  return isApiProblem(error) ? error : createUnknownApiProblem();
}
