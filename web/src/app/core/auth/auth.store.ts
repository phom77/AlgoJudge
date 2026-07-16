import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, EMPTY, finalize, map, of, tap, throwError } from 'rxjs';
import type { Observable } from 'rxjs';

import { AuthApiGateway } from '../api/auth-api.gateway';
import type { ApiProblem } from '../error/api-problem';
import { createUnknownApiProblem, isApiProblem } from '../error/api-problem';
import type { AuthPhase, AuthUser, LoginCredentials, RegistrationDetails } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly gateway = inject(AuthApiGateway);
  private readonly userState = signal<AuthUser | null>(null);
  private readonly phaseState = signal<AuthPhase>('checking');
  private readonly submittingState = signal(false);
  private readonly problemState = signal<ApiProblem | null>(null);

  readonly user = this.userState.asReadonly();
  readonly phase = this.phaseState.asReadonly();
  readonly isAuthenticated = computed(() => this.phaseState() === 'authenticated');
  readonly isChecking = computed(() => this.phaseState() === 'checking');
  readonly isSubmitting = this.submittingState.asReadonly();
  readonly problem = this.problemState.asReadonly();

  restoreSession(): Observable<void> {
    this.phaseState.set('checking');
    this.problemState.set(null);

    return this.gateway.session().pipe(
      catchError((error: unknown) => {
        const problem = asApiProblem(error);
        return problem.code === 'authentication'
          ? this.gateway.refresh()
          : throwError(() => problem);
      }),
      tap((user) => this.setAuthenticated(user)),
      map(() => undefined),
      catchError((error: unknown) => {
        this.setAnonymous(asApiProblem(error));
        return of(undefined);
      }),
    );
  }

  login(credentials: LoginCredentials): Observable<void> {
    return this.authenticate(this.gateway.login(credentials));
  }

  register(details: RegistrationDetails): Observable<void> {
    return this.authenticate(this.gateway.register(details));
  }

  refreshSession(): Observable<void> {
    return this.gateway.refresh().pipe(
      tap((user) => this.setAuthenticated(user)),
      map(() => undefined),
      catchError((error: unknown) => {
        const problem = asApiProblem(error);
        this.setAnonymous(problem);
        return throwError(() => problem);
      }),
    );
  }

  logout(): Observable<void> {
    this.submittingState.set(true);
    this.problemState.set(null);

    return this.gateway.revoke().pipe(
      catchError(() => of(undefined)),
      tap(() => this.setAnonymous()),
      finalize(() => this.submittingState.set(false)),
    );
  }

  clearProblem(): void {
    this.problemState.set(null);
  }

  private authenticate(request: Observable<AuthUser>): Observable<void> {
    this.submittingState.set(true);
    this.problemState.set(null);

    return request.pipe(
      tap((user) => this.setAuthenticated(user)),
      map(() => undefined),
      catchError((error: unknown) => {
        this.problemState.set(asApiProblem(error));
        return EMPTY;
      }),
      finalize(() => this.submittingState.set(false)),
    );
  }

  private setAuthenticated(user: AuthUser): void {
    this.userState.set(user);
    this.phaseState.set('authenticated');
    this.problemState.set(null);
  }

  private setAnonymous(problem: ApiProblem | null = null): void {
    this.userState.set(null);
    this.phaseState.set('anonymous');
    this.problemState.set(problem);
  }
}

function asApiProblem(error: unknown): ApiProblem {
  return isApiProblem(error) ? error : createUnknownApiProblem();
}
