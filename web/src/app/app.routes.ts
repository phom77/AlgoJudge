import type { Routes } from '@angular/router';

import { anonymousGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'problems',
  },
  {
    path: 'login',
    canActivate: [anonymousGuard],
    loadComponent: () =>
      import('./features/auth/login/login.page').then((module) => module.LoginPage),
    title: 'Sign in | AlgoJudge',
  },
  {
    path: 'register',
    canActivate: [anonymousGuard],
    loadComponent: () =>
      import('./features/auth/register/register.page').then((module) => module.RegisterPage),
    title: 'Create account | AlgoJudge',
  },
  {
    path: 'problems',
    loadComponent: () =>
      import('./features/problems/catalogue/problem-catalogue.page').then(
        (module) => module.ProblemCataloguePage,
      ),
    title: 'Problems | AlgoJudge',
  },
  {
    path: '**',
    redirectTo: 'problems',
  },
];
