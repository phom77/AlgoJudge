import { inject, Injectable } from '@angular/core';
import { catchError, finalize, map, shareReplay, switchMap, throwError } from 'rxjs';
import type { Observable } from 'rxjs';

import type { AuthUser, LoginCredentials, RegistrationDetails } from '../auth/auth.models';
import { mapProblemDetails } from './problem-details.mapper';
import { AntiforgeryService } from './antiforgery.service';
import { AlgoJudgeApi } from './generated/algo-judge-api';
import { apiAuthLoginPost$Json } from './generated/fn/auth/api-auth-login-post-json';
import { apiAuthRefreshPost$Json } from './generated/fn/auth/api-auth-refresh-post-json';
import { apiAuthRegisterPost$Json } from './generated/fn/auth/api-auth-register-post-json';
import { apiAuthRevokePost } from './generated/fn/auth/api-auth-revoke-post';
import { apiAuthSessionGet$Json } from './generated/fn/auth/api-auth-session-get-json';
import type { AuthResponse } from './generated/models/auth-response';

@Injectable({ providedIn: 'root' })
export class AuthApiGateway {
  private readonly api = inject(AlgoJudgeApi);
  private readonly antiforgery = inject(AntiforgeryService);
  private refreshRequest: Observable<AuthUser> | null = null;

  session(): Observable<AuthUser> {
    return this.api.invoke(apiAuthSessionGet$Json).pipe(
      map(toAuthUser),
      catchError((error: unknown) => this.rethrowProblem(error)),
    );
  }

  login(credentials: LoginCredentials): Observable<AuthUser> {
    return this.runUnsafe(() =>
      this.api
        .invoke(apiAuthLoginPost$Json, {
          body: credentials,
        })
        .pipe(map(toAuthUser)),
    );
  }

  register(details: RegistrationDetails): Observable<AuthUser> {
    return this.runUnsafe(() =>
      this.api
        .invoke(apiAuthRegisterPost$Json, {
          body: details,
        })
        .pipe(map(toAuthUser)),
    );
  }

  refresh(): Observable<AuthUser> {
    if (this.refreshRequest !== null) {
      return this.refreshRequest;
    }

    const request = this.runUnsafe(() =>
      this.api.invoke(apiAuthRefreshPost$Json).pipe(map(toAuthUser)),
    ).pipe(
      finalize(() => {
        this.refreshRequest = null;
      }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    this.refreshRequest = request;
    return request;
  }

  revoke(): Observable<void> {
    return this.runUnsafe(() => this.api.invoke(apiAuthRevokePost));
  }

  private runUnsafe<T>(request: () => Observable<T>): Observable<T> {
    return this.antiforgery.ensureToken().pipe(
      switchMap(request),
      catchError((error: unknown) => this.rethrowProblem(error)),
    );
  }

  private rethrowProblem(error: unknown): Observable<never> {
    const problem = mapProblemDetails(error);
    if (problem.code === 'csrf') {
      this.antiforgery.invalidate();
    }
    return throwError(() => problem);
  }
}

function toAuthUser(response: AuthResponse): AuthUser {
  const userName = readRequiredText(response.userName);
  const email = readRequiredText(response.email);
  const expiresAt = readRequiredText(response.expiresAt);

  if (Number.isNaN(Date.parse(expiresAt))) {
    throw new Error('The authentication response contains an invalid expiration timestamp.');
  }

  return { userName, email, expiresAt };
}

function readRequiredText(value: string | undefined): string {
  if (value === undefined || value.trim().length === 0) {
    throw new Error('The authentication response is incomplete.');
  }
  return value.trim();
}
