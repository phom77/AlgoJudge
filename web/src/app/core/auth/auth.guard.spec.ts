import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree } from '@angular/router';
import { provideRouter, Router } from '@angular/router';

import { adminGuard, anonymousGuard, authGuard } from './auth.guard';
import { AuthStore } from './auth.store';
import { normalizeReturnUrl } from './return-url';

describe('auth guards', () => {
  const authenticated = signal(false);
  const admin = signal(false);

  beforeEach(() => {
    authenticated.set(false);
    admin.set(false);
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { isAuthenticated: authenticated, isAdmin: admin } },
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

  it('redirects an authenticated regular user away from admin routes', () => {
    authenticated.set(true);
    const result = TestBed.runInInjectionContext(() =>
      adminGuard(
        {} as ActivatedRouteSnapshot,
        { url: '/admin/problems/new' } as RouterStateSnapshot,
      ),
    );

    expect(TestBed.inject(Router).serializeUrl(result as UrlTree)).toBe('/forbidden');
  });

  it('allows an authenticated admin to open admin routes', () => {
    authenticated.set(true);
    admin.set(true);
    const result = TestBed.runInInjectionContext(() =>
      adminGuard(
        {} as ActivatedRouteSnapshot,
        { url: '/admin/problems/new' } as RouterStateSnapshot,
      ),
    );

    expect(result).toBe(true);
  });

  it('rejects external and protocol-relative return URLs', () => {
    expect(normalizeReturnUrl('https://evil.example')).toBe('/problems');
    expect(normalizeReturnUrl('//evil.example')).toBe('/problems');
    expect(normalizeReturnUrl('/submissions')).toBe('/submissions');
  });
});
