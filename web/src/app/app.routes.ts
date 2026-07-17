import type { Routes } from '@angular/router';

import { anonymousGuard, authGuard } from './core/auth/auth.guard';

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
    path: 'problems/:slug',
    loadComponent: () =>
      import('./features/problems/workspace/problem-workspace.page').then(
        (module) => module.ProblemWorkspacePage,
      ),
    title: 'Problem workspace | AlgoJudge',
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
    path: 'submissions/:id',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/submissions/detail/submission-detail.page').then(
        (module) => module.SubmissionDetailPage,
      ),
    title: 'Submission result | AlgoJudge',
  },
  {
    path: 'submissions',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/submissions/history/submission-history.page').then(
        (module) => module.SubmissionHistoryPage,
      ),
    title: 'Submission history | AlgoJudge',
  },
  {
    path: '**',
    redirectTo: 'problems',
  },
];
