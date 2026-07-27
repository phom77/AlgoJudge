import type { Routes } from '@angular/router';

import { adminGuard, anonymousGuard, authGuard } from './core/auth/auth.guard';

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
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./core/layout/admin-shell/admin-shell.component').then(
        (module) => module.AdminShellComponent,
      ),
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'problems/new',
      },
      {
        path: 'problems/new',
        loadComponent: () =>
          import('./features/admin-authoring/problem-authoring.page').then(
            (module) => module.ProblemAuthoringPage,
          ),
        title: 'Create problem | AlgoJudge',
      },
      {
        path: 'problems/:revisionId/author',
        loadComponent: () =>
          import('./features/admin-authoring/problem-authoring.page').then(
            (module) => module.ProblemAuthoringPage,
          ),
        title: 'Problem authoring | AlgoJudge',
      },
    ],
  },
  {
    path: 'forbidden',
    loadComponent: () =>
      import('./features/errors/forbidden/forbidden.page').then((module) => module.ForbiddenPage),
    title: 'Access restricted | AlgoJudge',
  },
  {
    path: '**',
    redirectTo: 'problems',
  },
];
