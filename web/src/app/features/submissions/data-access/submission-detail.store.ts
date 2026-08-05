import { computed, DestroyRef, inject, Injectable, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { catchError, distinctUntilChanged, map, of, Subject, switchMap } from 'rxjs';
import type { Observable } from 'rxjs';

import type { ApiProblem } from '../../../core/error/api-problem';
import { createUnknownApiProblem, isApiProblem } from '../../../core/error/api-problem';
import { SubmissionGateway } from './submission.gateway';
import type { Submission, SubmissionContent } from './submission.models';
import { SubmissionPollingService } from './submission-polling.service';

interface DetailState {
  readonly id: string;
  readonly submission: Submission | null;
  readonly content: SubmissionContent | null;
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
    content: null,
    loading: true,
    problem: null,
  });

  readonly submission = computed(() => this.state().submission);
  readonly content = computed(() => this.state().content);
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
    this.state.set({ id, submission: null, content: null, loading: true, problem: null });
    if (!isSubmissionIdentifier(id)) return of(invalidIdState(id));
    return this.gateway.detail(id).pipe(
      switchMap((initial) =>
        this.gateway.content(id).pipe(
          switchMap((content) =>
            this.polling.watch(initial).pipe(
              switchMap((submission) => {
                if (submission.status === 'CompileError' && content.compileMessage === null) {
                  return this.gateway
                    .content(id)
                    .pipe(map((updatedContent) => ({ submission, content: updatedContent })));
                }
                return of({ submission, content });
              }),
            ),
          ),
        ),
      ),
      map(({ submission, content }) => ({
        id,
        submission,
        content,
        loading: false,
        problem: null,
      })),
      catchError((error: unknown) =>
        of({ id, submission: null, content: null, loading: false, problem: asApiProblem(error) }),
      ),
    );
  }
}

function invalidIdState(id: string): DetailState {
  return {
    id,
    submission: null,
    content: null,
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

export function isSubmissionIdentifier(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
}

function asApiProblem(error: unknown): ApiProblem {
  return isApiProblem(error) ? error : createUnknownApiProblem();
}
