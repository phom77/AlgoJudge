import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';

import { AlgoJudgeApi } from './generated/algo-judge-api';
import { AntiforgeryService } from './antiforgery.service';

describe('AntiforgeryService', () => {
  const invoke = vi.fn();

  beforeEach(() => {
    invoke.mockReset();
    TestBed.configureTestingModule({
      providers: [{ provide: AlgoJudgeApi, useValue: { invoke } }],
    });
  });

  it('shares and caches the CSRF bootstrap request', () => {
    const response = new Subject<void>();
    invoke.mockReturnValue(response.asObservable());
    const service = TestBed.inject(AntiforgeryService);

    service.ensureToken().subscribe();
    service.ensureToken().subscribe();

    expect(invoke).toHaveBeenCalledTimes(1);
    response.next();
    response.complete();
    service.ensureToken().subscribe();
    expect(invoke).toHaveBeenCalledTimes(1);
  });

  it('allows bootstrap to retry after a failure', () => {
    invoke
      .mockReturnValueOnce(throwError(() => new Error('offline')))
      .mockReturnValueOnce(of(undefined));
    const service = TestBed.inject(AntiforgeryService);

    service.ensureToken().subscribe({ error: () => undefined });
    service.ensureToken().subscribe();

    expect(invoke).toHaveBeenCalledTimes(2);
  });
});
