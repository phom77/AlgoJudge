import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { map } from 'rxjs';

import { PaginationComponent } from '../../../shared/ui/pagination/pagination.component';
import { SubmissionHistoryStore } from '../data-access/submission-history.store';
import { readSubmissionQuery, writeSubmissionQuery } from '../data-access/submission-history-query';
import type { SubmissionHistoryQuery, SubmissionStatus } from '../data-access/submission.models';
import { SubmissionHistoryFiltersComponent } from './submission-history-filters.component';
import { SubmissionHistoryTableComponent } from './submission-history-table.component';

@Component({
  selector: 'aj-submission-history-page',
  imports: [
    PaginationComponent,
    SubmissionHistoryFiltersComponent,
    SubmissionHistoryTableComponent,
  ],
  providers: [SubmissionHistoryStore],
  templateUrl: './submission-history.page.html',
  styleUrl: './submission-history.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubmissionHistoryPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly store = inject(SubmissionHistoryStore);

  constructor() {
    this.store.connect(this.route.queryParamMap.pipe(map(readSubmissionQuery)));
  }

  protected updateProblemId(problemId: number | null): void {
    this.navigate({ problemId, pageNumber: 1 });
  }

  protected updateStatus(status: SubmissionStatus | null): void {
    this.navigate({ status, pageNumber: 1 });
  }

  protected updatePage(pageNumber: number): void {
    this.navigate({ pageNumber });
  }

  protected updatePageSize(event: Event): void {
    const pageSize = Number((event.target as HTMLSelectElement).value);
    this.navigate({ pageSize, pageNumber: 1 });
  }

  private navigate(patch: Partial<SubmissionHistoryQuery>): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: writeSubmissionQuery({ ...this.store.query(), ...patch }),
      replaceUrl: true,
    });
  }
}
