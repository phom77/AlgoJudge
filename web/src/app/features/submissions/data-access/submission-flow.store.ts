import { computed, inject, Injectable, signal } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { catchError, EMPTY, filter, finalize, shareReplay, switchMap, takeUntil, tap } from 'rxjs';
import type { Observable } from 'rxjs';

import { AuthStore } from '../../../core/auth/auth.store';
import type { ApiProblem } from '../../../core/error/api-problem';
import { createUnknownApiProblem, isApiProblem } from '../../../core/error/api-problem';
import { SubmissionGateway } from './submission.gateway';
import { isTerminalStatus, type Submission } from './submission.models';
import { SubmissionPollingService } from './submission-polling.service';

type SubmissionPhase = 'idle' | 'creating' | 'polling' | 'complete';

@Injectable()
export class SubmissionFlowStore {
  private readonly authStore = inject(AuthStore);
  private readonly gateway = inject(SubmissionGateway);
  private readonly polling = inject(SubmissionPollingService);
  private readonly logout$ = toObservable(this.authStore.isAuthenticated).pipe(
    filter((authenticated) => !authenticated),
  );
  private readonly phaseState = signal<SubmissionPhase>('idle');
  private readonly submissionState = signal<Submission | null>(null);
  private readonly problemState = signal<ApiProblem | null>(null);
  private activeRequest: Observable<Submission> | null = null;

  readonly phase = this.phaseState.asReadonly();
  readonly submission = this.submissionState.asReadonly();
  readonly problem = this.problemState.asReadonly();
  readonly submitting = computed(() => {
    const phase = this.phaseState();
    return phase === 'creating' || phase === 'polling';
  });

  submit(problemId: number, sourceCode: string): Observable<Submission> {
    if (this.activeRequest !== null) return EMPTY;
    const validationProblem = validateSubmission(problemId, sourceCode);
    if (validationProblem !== null) {
      this.problemState.set(validationProblem);
      return EMPTY;
    }

    this.phaseState.set('creating');
    this.problemState.set(null);
    this.submissionState.set(null);
    const request = this.gateway.create(problemId, sourceCode).pipe(
      tap((submission) => {
        this.submissionState.set(submission);
        this.phaseState.set(isTerminalStatus(submission.status) ? 'complete' : 'polling');
      }),
      switchMap((submission) => this.polling.watch(submission)),
      tap((submission) => {
        this.submissionState.set(submission);
        this.phaseState.set(isTerminalStatus(submission.status) ? 'complete' : 'polling');
      }),
      takeUntil(this.logout$),
      catchError((error: unknown) => {
        this.phaseState.set('idle');
        this.problemState.set(asApiProblem(error));
        return EMPTY;
      }),
      finalize(() => {
        this.activeRequest = null;
        const submission = this.submissionState();
        if (submission === null || !isTerminalStatus(submission.status))
          this.phaseState.set('idle');
      }),
      shareReplay({ bufferSize: 1, refCount: true }),
    );
    this.activeRequest = request;
    return request;
  }

  clearResult(): void {
    if (this.submitting()) return;
    this.phaseState.set('idle');
    this.submissionState.set(null);
    this.problemState.set(null);
  }
}

function validateSubmission(problemId: number, sourceCode: string): ApiProblem | null {
  const sourceBytes = new TextEncoder().encode(sourceCode).byteLength;
  if (
    Number.isInteger(problemId) &&
    problemId > 0 &&
    sourceCode.trim().length > 0 &&
    sourceBytes <= 65_536
  ) {
    return null;
  }
  return {
    status: 400,
    code: 'validation',
    title: 'The submission is invalid.',
    detail:
      sourceBytes > 65_536
        ? 'Source code must not exceed 65,536 UTF-8 bytes.'
        : 'Source code is required.',
    type: null,
    instance: null,
    traceId: null,
    validationErrors: {},
    retryAfterSeconds: null,
  };
}

function asApiProblem(error: unknown): ApiProblem {
  return isApiProblem(error) ? error : createUnknownApiProblem();
}
