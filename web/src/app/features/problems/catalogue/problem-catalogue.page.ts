import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { combineLatest, map, tap } from 'rxjs';

import { AuthStore } from '../../../core/auth/auth.store';
import { PaginationComponent } from '../../../shared/ui/pagination/pagination.component';
import { ProblemCatalogueStore } from '../data-access/problem-catalogue.store';
import { readProblemQuery, writeProblemQuery } from '../data-access/problem-query';
import type { ProblemCatalogueQuery, ProblemDifficulty } from '../data-access/problem.models';
import { ProblemFiltersComponent } from './problem-filters.component';
import { ProblemTableComponent } from './problem-table.component';

@Component({
  selector: 'aj-problem-catalogue-page',
  imports: [PaginationComponent, ProblemFiltersComponent, ProblemTableComponent],
  providers: [ProblemCatalogueStore],
  templateUrl: './problem-catalogue.page.html',
  styleUrl: './problem-catalogue.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProblemCataloguePage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly authStore = inject(AuthStore);
  protected readonly store = inject(ProblemCatalogueStore);

  constructor() {
    const query$ = combineLatest([
      this.route.queryParamMap,
      toObservable(this.authStore.isAuthenticated),
    ]).pipe(
      tap(([params, authenticated]) => {
        if (!authenticated && params.has('solved')) this.removeAnonymousSolvedFilter();
      }),
      map(([params, authenticated]) => readProblemQuery(params, authenticated)),
    );
    this.store.connect(query$);
  }

  protected updateSearch(search: string): void {
    this.navigate({ search, pageNumber: 1 });
  }

  protected updateDifficulty(difficulty: ProblemDifficulty | null): void {
    this.navigate({ difficulty, pageNumber: 1 });
  }

  protected updateSolved(solved: boolean | null): void {
    this.navigate({ solved, pageNumber: 1 });
  }

  protected toggleTag(slug: string): void {
    const tags = this.store.query().tags.includes(slug)
      ? this.store.query().tags.filter((tag) => tag !== slug)
      : [...this.store.query().tags, slug].slice(0, 10);
    this.navigate({ tags, pageNumber: 1 });
  }

  protected updatePage(pageNumber: number): void {
    this.navigate({ pageNumber });
  }

  protected updatePageSize(event: Event): void {
    const pageSize = Number((event.target as HTMLSelectElement).value);
    this.navigate({ pageSize, pageNumber: 1 });
  }

  private navigate(patch: Partial<ProblemCatalogueQuery>): void {
    const query = { ...this.store.query(), ...patch };
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: writeProblemQuery(query),
      replaceUrl: true,
    });
  }

  private removeAnonymousSolvedFilter(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { solved: null },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
}
