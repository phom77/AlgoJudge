import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { authRefreshInterceptor } from './auth-refresh.interceptor';
import { AuthStore } from './auth.store';

describe('authRefreshInterceptor', () => {
  const refreshSession = vi.fn();

  beforeEach(() => {
    refreshSession.mockReset();
    refreshSession.mockReturnValue(of(undefined));
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authRefreshInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthStore, useValue: { refreshSession } },
      ],
    });
  });

  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('refreshes and retries one safe API request after a 401', () => {
    const http = TestBed.inject(HttpClient);
    const controller = TestBed.inject(HttpTestingController);
    let response: unknown;

    http.get('/api/problems').subscribe((value: unknown) => (response = value));
    controller.expectOne('/api/problems').flush(null, { status: 401, statusText: 'Unauthorized' });
    controller.expectOne('/api/problems').flush({ ok: true });

    expect(refreshSession).toHaveBeenCalledTimes(1);
    expect(response).toEqual({ ok: true });
  });

  it('never replays a submission POST', () => {
    const http = TestBed.inject(HttpClient);
    const controller = TestBed.inject(HttpTestingController);

    http.post('/api/submissions', {}).subscribe({ error: () => undefined });
    controller
      .expectOne('/api/submissions')
      .flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(refreshSession).not.toHaveBeenCalled();
  });
});
