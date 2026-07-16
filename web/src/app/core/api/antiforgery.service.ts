import { inject, Injectable } from '@angular/core';
import { catchError, shareReplay, throwError } from 'rxjs';
import type { Observable } from 'rxjs';

import { AlgoJudgeApi } from './generated/algo-judge-api';
import { apiAuthCsrfGet } from './generated/fn/auth/api-auth-csrf-get';

@Injectable({ providedIn: 'root' })
export class AntiforgeryService {
  private readonly api = inject(AlgoJudgeApi);
  private tokenRequest: Observable<void> | null = null;

  ensureToken(): Observable<void> {
    if (this.tokenRequest !== null) {
      return this.tokenRequest;
    }

    this.tokenRequest = this.api.invoke(apiAuthCsrfGet).pipe(
      catchError((error: unknown) => {
        this.tokenRequest = null;
        return throwError(() => error);
      }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.tokenRequest;
  }

  invalidate(): void {
    this.tokenRequest = null;
  }
}
