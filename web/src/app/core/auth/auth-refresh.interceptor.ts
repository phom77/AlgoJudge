import { HttpContextToken, HttpErrorResponse, type HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';

import { AuthStore } from './auth.store';

const AUTH_RETRY_ATTEMPTED = new HttpContextToken(() => false);
const SAFE_REPLAY_METHODS = new Set(['GET', 'HEAD', 'OPTIONS']);

export const authRefreshInterceptor: HttpInterceptorFn = (request, next) => {
  const store = inject(AuthStore);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (
        !shouldRefresh(
          request.url,
          request.method,
          request.context.get(AUTH_RETRY_ATTEMPTED),
          error,
        )
      ) {
        return throwError(() => error);
      }

      return store.refreshSession().pipe(
        switchMap(() =>
          next(request.clone({ context: request.context.set(AUTH_RETRY_ATTEMPTED, true) })),
        ),
        catchError(() => throwError(() => error)),
      );
    }),
  );
};

function shouldRefresh(
  url: string,
  method: string,
  retryAttempted: boolean,
  error: unknown,
): boolean {
  return (
    error instanceof HttpErrorResponse &&
    error.status === 401 &&
    url.startsWith('/api/') &&
    !url.startsWith('/api/auth/') &&
    SAFE_REPLAY_METHODS.has(method.toUpperCase()) &&
    !retryAttempted
  );
}
