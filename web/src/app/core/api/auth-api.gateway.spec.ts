import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of, Subject, throwError } from 'rxjs';

import { AntiforgeryService } from './antiforgery.service';
import { AuthApiGateway } from './auth-api.gateway';
import { AlgoJudgeApi } from './generated/algo-judge-api';

describe('AuthApiGateway', () => {
  const invoke = vi.fn();
  const ensureToken = vi.fn();
  const invalidate = vi.fn();

  beforeEach(() => {
    invoke.mockReset();
    ensureToken.mockReset();
    invalidate.mockReset();
    ensureToken.mockReturnValue(of(undefined));

    TestBed.configureTestingModule({
      providers: [
        { provide: AlgoJudgeApi, useValue: { invoke } },
        { provide: AntiforgeryService, useValue: { ensureToken, invalidate } },
      ],
    });
  });

  it('bootstraps CSRF before login and maps the public user response', async () => {
    invoke.mockReturnValue(
      of({ userName: 'ada', email: 'ada@example.com', expiresAt: '2026-07-17T10:00:00Z' }),
    );
    const gateway = TestBed.inject(AuthApiGateway);

    const user = await firstValueFrom(gateway.login({ userName: 'ada', password: 'secret1' }));

    expect(ensureToken).toHaveBeenCalledTimes(1);
    expect(user).toEqual({
      userName: 'ada',
      email: 'ada@example.com',
      expiresAt: '2026-07-17T10:00:00Z',
    });
  });

  it('shares one refresh request across concurrent subscribers', () => {
    const response = new Subject<{
      userName: string;
      email: string;
      expiresAt: string;
    }>();
    invoke.mockReturnValue(response.asObservable());
    const gateway = TestBed.inject(AuthApiGateway);

    const first = gateway.refresh();
    const second = gateway.refresh();
    first.subscribe();
    second.subscribe();

    expect(first).toBe(second);
    expect(ensureToken).toHaveBeenCalledTimes(1);
    expect(invoke).toHaveBeenCalledTimes(1);
    response.next({
      userName: 'ada',
      email: 'ada@example.com',
      expiresAt: '2026-07-17T10:00:00Z',
    });
    response.complete();
  });

  it('invalidates cached antiforgery state after a CSRF rejection', async () => {
    invoke.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 403,
            error: { code: 'csrf', title: 'Request verification failed.' },
          }),
      ),
    );
    const gateway = TestBed.inject(AuthApiGateway);

    await firstValueFrom(gateway.login({ userName: 'ada', password: 'secret1' })).catch(
      () => undefined,
    );

    expect(invalidate).toHaveBeenCalledTimes(1);
  });
});
