import { inject } from '@angular/core';
import type { CanActivateFn } from '@angular/router';
import { Router } from '@angular/router';

import { AuthStore } from './auth.store';

export const authGuard: CanActivateFn = (_route, state) => {
  const store = inject(AuthStore);
  const router = inject(Router);

  return store.isAuthenticated()
    ? true
    : router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

export const anonymousGuard: CanActivateFn = () => {
  const store = inject(AuthStore);
  const router = inject(Router);

  return store.isAuthenticated() ? router.createUrlTree(['/problems']) : true;
};
