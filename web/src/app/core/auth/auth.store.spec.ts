import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of, throwError } from 'rxjs';
import type { Observable } from 'rxjs';

import { AuthApiGateway } from '../api/auth-api.gateway';
import type { ApiProblem, ApiProblemCode } from '../error/api-problem';
import type { AuthUser } from './auth.models';
import { AuthStore } from './auth.store';

describe('AuthStore', () => {
  const session = vi.fn();
  const login = vi.fn();
  const register = vi.fn();
  const refresh = vi.fn();
  const revoke = vi.fn();
  const user: AuthUser = {
    userName: 'ada',
    email: 'ada@example.com',
    expiresAt: '2026-07-17T10:00:00Z',
  };

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AuthApiGateway,
          useValue: { session, login, register, refresh, revoke },
        },
      ],
    });
  });

  it('restores an existing browser session', async () => {
    session.mockReturnValue(of(user));
    const store = TestBed.inject(AuthStore);

    await firstValueFrom(store.restoreSession());

    expect(store.user()).toEqual(user);
    expect(store.isAuthenticated()).toBe(true);
  });

  it('refreshes once when session restore receives authentication failure', async () => {
    session.mockReturnValue(throwError(() => problem('authentication', 401)));
    refresh.mockReturnValue(of(user));
    const store = TestBed.inject(AuthStore);

    await firstValueFrom(store.restoreSession());

    expect(refresh).toHaveBeenCalledTimes(1);
    expect(store.user()).toEqual(user);
  });

  it('becomes anonymous when session restore cannot refresh', async () => {
    session.mockReturnValue(throwError(() => problem('authentication', 401)));
    refresh.mockReturnValue(throwError(() => problem('authentication', 401)));
    const store = TestBed.inject(AuthStore);

    await firstValueFrom(store.restoreSession());

    expect(store.phase()).toBe('anonymous');
    expect(store.user()).toBeNull();
  });

  it('exposes login Problem Details without storing credentials', async () => {
    login.mockReturnValue(throwError(() => problem('authentication', 401)));
    const store = TestBed.inject(AuthStore);

    await completes(store.login({ userName: 'ada', password: 'not-stored' }));

    expect(store.problem()?.code).toBe('authentication');
    expect(JSON.stringify(store)).not.toContain('not-stored');
  });

  it('clears local state even when revoke fails', async () => {
    login.mockReturnValue(of(user));
    revoke.mockReturnValue(throwError(() => problem('network', 0)));
    const store = TestBed.inject(AuthStore);
    await firstValueFrom(store.login({ userName: 'ada', password: 'secret1' }));

    await firstValueFrom(store.logout());

    expect(store.phase()).toBe('anonymous');
    expect(store.user()).toBeNull();
  });
});

function completes(observable: Observable<void>): Promise<void> {
  return new Promise((resolve, reject) =>
    observable.subscribe({ complete: resolve, error: reject }),
  );
}

function problem(code: ApiProblemCode, status: number): ApiProblem {
  return {
    status,
    code,
    title: 'Request failed.',
    detail: null,
    type: null,
    instance: null,
    traceId: null,
    validationErrors: {},
    retryAfterSeconds: null,
  };
}
