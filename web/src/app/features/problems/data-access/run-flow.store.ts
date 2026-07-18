import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, EMPTY, filter, finalize, shareReplay, switchMap, takeUntil, tap } from 'rxjs';
import { toObservable } from '@angular/core/rxjs-interop';
import type { Observable } from 'rxjs';
import type { ApiProblem } from '../../../core/error/api-problem';
import { createUnknownApiProblem, isApiProblem } from '../../../core/error/api-problem';
import { RunGateway } from './run.gateway';
import { isTerminalRunStatus, type CodeRun, type RunInput } from './run.models';
import { RunPollingService } from './run-polling.service';
import { AuthStore } from '../../../core/auth/auth.store';

type RunPhase = 'idle' | 'creating' | 'polling' | 'complete';

@Injectable()
export class RunFlowStore {
  private readonly gateway = inject(RunGateway);
  private readonly polling = inject(RunPollingService);
  private readonly authStore = inject(AuthStore);
  private readonly logout$ = toObservable(this.authStore.isAuthenticated).pipe(
    filter((authenticated) => !authenticated),
  );
  private readonly phaseState = signal<RunPhase>('idle');
  private readonly runState = signal<CodeRun | null>(null);
  private readonly problemState = signal<ApiProblem | null>(null);
  private activeRequest: Observable<CodeRun> | null = null;

  readonly phase = this.phaseState.asReadonly();
  readonly run = this.runState.asReadonly();
  readonly problem = this.problemState.asReadonly();
  readonly running = computed(
    () => this.phaseState() === 'creating' || this.phaseState() === 'polling',
  );

  execute(slug: string, sourceCode: string, input: RunInput): Observable<CodeRun> {
    if (this.activeRequest !== null) return EMPTY;
    if (!slug || !sourceCode.trim() || new TextEncoder().encode(sourceCode).byteLength > 65_536) {
      this.problemState.set(validationProblem());
      return EMPTY;
    }
    this.phaseState.set('creating');
    this.runState.set(null);
    this.problemState.set(null);
    const request = this.gateway.create(slug, sourceCode, input).pipe(
      tap((run) => this.update(run)),
      switchMap((run) => this.polling.watch(run)),
      tap((run) => this.update(run)),
      takeUntil(this.logout$),
      catchError((error: unknown) => {
        this.phaseState.set('idle');
        this.problemState.set(isApiProblem(error) ? error : createUnknownApiProblem());
        return EMPTY;
      }),
      finalize(() => {
        this.activeRequest = null;
        if (!this.runState()) this.phaseState.set('idle');
      }),
      shareReplay({ bufferSize: 1, refCount: true }),
    );
    this.activeRequest = request;
    return request;
  }

  clearResult(): void {
    if (this.running()) return;
    this.phaseState.set('idle');
    this.runState.set(null);
    this.problemState.set(null);
  }

  private update(run: CodeRun): void {
    this.runState.set(run);
    this.phaseState.set(isTerminalRunStatus(run.status) ? 'complete' : 'polling');
  }
}

function validationProblem(): ApiProblem {
  return {
    status: 400,
    code: 'validation',
    title: 'The run is invalid.',
    detail: 'A valid problem and C++17 source within 65,536 UTF-8 bytes are required.',
    type: null,
    instance: null,
    traceId: null,
    validationErrors: {},
    retryAfterSeconds: null,
  };
}
