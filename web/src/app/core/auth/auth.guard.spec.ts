import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree } from '@angular/router';
import { provideRouter, Router } from '@angular/router';

import { authGuard, anonymousGuard } from './auth.guard';
import { AuthStore } from './auth.store';
import { normalizeReturnUrl } from './return-url';

describe('auth guards', () => {
  const authenticated = signal(false);

  beforeEach(() => {
    authenticated.set(false);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { isAuthenticated: authenticated } },
      ],
    });
  });

  it('redirects anonymous users to login with an internal return URL', () => {
    const result = TestBed.runInInjectionContext(() =>
      authGuard({} as ActivatedRouteSnapshot, { url: '/submissions/7' } as RouterStateSnapshot),
    );

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe(
      '/login?returnUrl=%2Fsubmissions%2F7',
    );
  });

  it('keeps authenticated users away from anonymous-only routes', () => {
    authenticated.set(true);
    const result = TestBed.runInInjectionContext(() =>
      anonymousGuard({} as ActivatedRouteSnapshot, {} as RouterStateSnapshot),
    );

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/problems');
  });

  it('rejects external and protocol-relative return URLs', () => {
    expect(normalizeReturnUrl('https://evil.example')).toBe('/problems');
    expect(normalizeReturnUrl('//evil.example')).toBe('/problems');
    expect(normalizeReturnUrl('/submissions')).toBe('/submissions');
  });
});
