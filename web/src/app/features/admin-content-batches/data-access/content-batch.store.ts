import { computed, inject, Injectable, signal } from '@angular/core';
import { catchError, finalize, map, of, tap } from 'rxjs';
import type { Observable } from 'rxjs';

import type { ContentBatchListItemResponse } from '../../../core/api/admin-generated/models/content-batch-list-item-response';
import type { ContentBatchResponse } from '../../../core/api/admin-generated/models/content-batch-response';
import type { ApiProblem } from '../../../core/error/api-problem';
import { createUnknownApiProblem, isApiProblem } from '../../../core/error/api-problem';
import { ContentBatchGateway } from './content-batch.gateway';
import {
  INITIAL_CONTENT_BATCH_ITEM_QUERY,
  isActiveBatchStatus,
  isBatchStatus,
  isItemStatus,
  isRetryableItem,
  matchesItemQuery,
  type ContentBatchItemQuery,
} from './content-batch.models';
import { ContentBatchPollingService } from './content-batch-polling.service';

interface ContentBatchState {
  readonly batches: readonly ContentBatchListItemResponse[];
  readonly totalCount: number;
  readonly detail: ContentBatchResponse | null;
  readonly query: ContentBatchItemQuery;
  readonly selectedRevisionIds: readonly string[];
  readonly loading: boolean;
  readonly action: string | null;
  readonly problem: ApiProblem | null;
  readonly unchangedPolls: number;
  readonly fingerprint: string | null;
}

@Injectable()
export class ContentBatchStore {
  private readonly gateway = inject(ContentBatchGateway);
  private readonly polling = inject(ContentBatchPollingService);
  private readonly state = signal<ContentBatchState>({
    batches: [],
    totalCount: 0,
    detail: null,
    query: INITIAL_CONTENT_BATCH_ITEM_QUERY,
    selectedRevisionIds: [],
    loading: false,
    action: null,
    problem: null,
    unchangedPolls: 0,
    fingerprint: null,
  });

  readonly batches = computed(() => this.state().batches);
  readonly totalCount = computed(() => this.state().totalCount);
  readonly detail = computed(() => this.state().detail);
  readonly query = computed(() => this.state().query);
  readonly selectedRevisionIds = computed(() => this.state().selectedRevisionIds);
  readonly loading = computed(() => this.state().loading);
  readonly action = computed(() => this.state().action);
  readonly problem = computed(() => this.state().problem);
  readonly visibleItems = computed(() =>
    (this.state().detail?.items ?? []).filter((item) => matchesItemQuery(item, this.state().query)),
  );
  readonly failedItemIds = computed(() =>
    (this.state().detail?.items ?? [])
      .filter((item) => isRetryableItem(item) && item.id)
      .map((item) => item.id!),
  );
  readonly readyRevisionIds = computed(() =>
    (this.state().detail?.items ?? [])
      .filter((item) => isItemStatus(item.status, 'Ready') && item.revisionId)
      .map((item) => item.revisionId!),
  );
  readonly workerUnavailable = computed(() => {
    const problem = this.state().problem;
    return (
      this.state().unchangedPolls >= 12 ||
      (problem !== null && (problem.code === 'network' || problem.code === 'internal'))
    );
  });

  loadList(): Observable<void> {
    this.patch({ loading: true, problem: null });
    return this.gateway.list().pipe(
      tap((page) =>
        this.patch({
          batches: page.items ?? [],
          totalCount: Number(page.totalCount ?? 0),
        }),
      ),
      map(() => undefined),
      catchError((error: unknown) => this.handle(error)),
      finalize(() => this.patch({ loading: false })),
    );
  }

  loadDetail(batchId: string): Observable<void> {
    this.patch({ loading: true, problem: null });
    return this.gateway.get(batchId).pipe(
      tap((batch) => this.applyDetail(batch, false)),
      map(() => undefined),
      catchError((error: unknown) => this.handle(error)),
      finalize(() => this.patch({ loading: false })),
    );
  }

  watch(batchId: string): Observable<void> {
    return this.polling.watch(batchId).pipe(
      tap((batch) => this.applyDetail(batch, true)),
      map(() => undefined),
      catchError((error: unknown) => this.handle(error)),
    );
  }

  setQuery(query: ContentBatchItemQuery): void {
    this.patch({ query });
  }

  toggleRevision(revisionId: string, selected: boolean): void {
    const values = new Set(this.state().selectedRevisionIds);
    if (selected) values.add(revisionId);
    else values.delete(revisionId);
    this.patch({ selectedRevisionIds: [...values] });
  }

  selectAllReady(selected: boolean): void {
    this.patch({ selectedRevisionIds: selected ? this.readyRevisionIds() : [] });
  }

  startOrResume(batchId: string): Observable<boolean> {
    const source = isBatchStatus(this.state().detail?.status, 'Created')
      ? this.gateway.start(batchId)
      : this.gateway.resume(batchId);
    return this.run('resume', source);
  }

  retry(batchId: string, itemIds: readonly string[]): Observable<boolean> {
    if (itemIds.length === 0) return of(false);
    return this.run('retry', this.gateway.retry(batchId, itemIds));
  }

  publish(batchId: string): Observable<boolean> {
    const revisionIds = this.state().selectedRevisionIds;
    if (revisionIds.length === 0) return of(false);
    return this.run('publish', this.gateway.publish(batchId, revisionIds));
  }

  private run(action: string, source: Observable<ContentBatchResponse>): Observable<boolean> {
    this.patch({ action, problem: null });
    return source.pipe(
      tap((batch) => this.applyDetail(batch, false)),
      map(() => true),
      catchError((error: unknown) => {
        this.setProblem(error);
        return of(false);
      }),
      finalize(() => this.patch({ action: null })),
    );
  }

  private applyDetail(batch: ContentBatchResponse, polled: boolean): void {
    const fingerprint = [
      batch.status,
      batch.updatedAt,
      batch.counts?.pending,
      batch.counts?.generating,
      batch.counts?.ready,
      batch.counts?.failed,
      batch.counts?.published,
      batch.counts?.skipped,
    ].join('|');
    const unchangedPolls =
      polled && this.state().fingerprint === fingerprint && isActiveBatchStatus(batch.status)
        ? this.state().unchangedPolls + 1
        : 0;
    const ready = new Set(
      (batch.items ?? [])
        .filter((item) => isItemStatus(item.status, 'Ready') && item.revisionId)
        .map((item) => item.revisionId!),
    );
    this.patch({
      detail: batch,
      selectedRevisionIds: this.state().selectedRevisionIds.filter((id) => ready.has(id)),
      unchangedPolls,
      fingerprint,
      problem: null,
    });
  }

  private handle(error: unknown): Observable<void> {
    this.setProblem(error);
    return of(undefined);
  }

  private setProblem(error: unknown): void {
    this.patch({ problem: isApiProblem(error) ? error : createUnknownApiProblem() });
  }

  private patch(patch: Partial<ContentBatchState>): void {
    this.state.update((state) => ({ ...state, ...patch }));
  }
}
