import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';

import type { AdminProblemListItemResponse } from '../../core/api/admin-generated/models/admin-problem-list-item-response';
import {
  problemStatusLabel,
  revisionStatusLabel,
  type AdminProblemStatus,
} from './data-access/problem-management.models';
import { ProblemManagementStore } from './data-access/problem-management.store';

@Component({
  selector: 'aj-problem-management-page',
  imports: [DatePipe, RouterLink],
  providers: [ProblemManagementStore],
  templateUrl: './problem-management.page.html',
  styleUrl: './problem-management.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProblemManagementPage {
  private readonly destroyRef = inject(DestroyRef);
  private readonly router = inject(Router);
  protected readonly store = inject(ProblemManagementStore);
  protected readonly problemStatusLabel = problemStatusLabel;
  protected readonly revisionStatusLabel = revisionStatusLabel;

  constructor() {
    this.store.load().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  protected search(value: string): void {
    this.store
      .load({ ...this.store.query(), search: value })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe();
  }

  protected filterStatus(value: string): void {
    this.store
      .load({ ...this.store.query(), status: value as AdminProblemStatus })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe();
  }

  protected openRevision(item: AdminProblemListItemResponse): void {
    if (!item.latestRevisionId) return;
    void this.router.navigate(['/admin/problems', item.latestRevisionId, 'author']);
  }

  protected createRevision(item: AdminProblemListItemResponse): void {
    const problemId = Number(item.id);
    if (!Number.isInteger(problemId)) return;
    this.store
      .createRevision(problemId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((draft) => {
        if (draft?.revisionId)
          void this.router.navigate(['/admin/problems', draft.revisionId, 'author']);
      });
  }

  protected archive(item: AdminProblemListItemResponse): void {
    const problemId = Number(item.id);
    if (Number.isInteger(problemId))
      this.store.archive(problemId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  protected restore(item: AdminProblemListItemResponse): void {
    const problemId = Number(item.id);
    if (Number.isInteger(problemId))
      this.store.restore(problemId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  protected isAction(item: AdminProblemListItemResponse): boolean {
    return this.store.actionProblemId() === Number(item.id);
  }

  protected countByStatus(status: number): number {
    return this.store.items().filter((item) => item.status === status).length;
  }
}
