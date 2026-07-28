import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import type { ContentBatchItemResponse } from '../../core/api/admin-generated/models/content-batch-item-response';
import type { ContentBatchCountsResponse } from '../../core/api/admin-generated/models/content-batch-counts-response';
import { ContentBatchStore } from './data-access/content-batch.store';
import {
  batchProblemMessage,
  batchStatusLabel,
  importActionLabel,
  isActiveBatchStatus,
  isBatchStatus,
  isItemStatus,
  isRetryableItem,
  itemStatusLabel,
  type ContentBatchFilter,
} from './data-access/content-batch.models';

@Component({
  selector: 'aj-content-batch-detail-page',
  imports: [DatePipe, RouterLink],
  providers: [ContentBatchStore],
  templateUrl: './content-batch-detail.page.html',
  styleUrl: './content-batch-detail.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContentBatchDetailPage {
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private polling = false;
  protected readonly store = inject(ContentBatchStore);
  protected readonly batchStatusLabel = batchStatusLabel;
  protected readonly itemStatusLabel = itemStatusLabel;
  protected readonly importActionLabel = importActionLabel;
  protected readonly isBatchStatus = isBatchStatus;
  protected readonly isItemStatus = isItemStatus;
  protected readonly isRetryableItem = isRetryableItem;
  protected readonly batchProblemMessage = batchProblemMessage;
  protected readonly batchId = this.route.snapshot.paramMap.get('batchId') ?? '';

  constructor() {
    if (!this.batchId) return;
    this.store
      .loadDetail(this.batchId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.startPolling());
  }

  protected search(value: string): void {
    this.store.setQuery({ ...this.store.query(), search: value });
  }

  protected filter(value: string): void {
    this.store.setQuery({ ...this.store.query(), status: value as ContentBatchFilter });
  }

  protected retry(item: ContentBatchItemResponse): void {
    if (!item.id) return;
    this.store
      .retry(this.batchId, [item.id])
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((completed) => {
        if (completed) this.startPolling();
      });
  }

  protected retryAll(): void {
    this.store
      .retry(this.batchId, this.store.failedItemIds())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((completed) => {
        if (completed) this.startPolling();
      });
  }

  protected startOrResume(): void {
    this.store
      .startOrResume(this.batchId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((completed) => {
        if (completed) this.startPolling();
      });
  }

  protected toggle(item: ContentBatchItemResponse, selected: boolean): void {
    if (item.revisionId) this.store.toggleRevision(item.revisionId, selected);
  }

  protected selected(revisionId: string | null | undefined): boolean {
    return !!revisionId && this.store.selectedRevisionIds().includes(revisionId);
  }

  protected selectAll(selected: boolean): void {
    this.store.selectAllReady(selected);
  }

  protected publish(): void {
    const count = this.store.selectedRevisionIds().length;
    if (count === 0 || !confirm(`Publish ${count} explicitly selected revision(s)?`)) return;
    this.store
      .publish(this.batchId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((completed) => {
        if (completed) this.startPolling();
      });
  }

  protected hasPartialFailure(counts: ContentBatchCountsResponse | undefined): boolean {
    return Number(counts?.failed ?? 0) > 0 && Number(counts?.ready ?? 0) > 0;
  }

  private startPolling(): void {
    if (this.polling || !isActiveBatchStatus(this.store.detail()?.status)) return;
    this.polling = true;
    this.store
      .watch(this.batchId)
      .pipe(
        finalize(() => (this.polling = false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }
}
