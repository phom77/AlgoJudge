import type { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'problems',
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
