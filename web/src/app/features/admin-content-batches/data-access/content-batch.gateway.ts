import { inject, Injectable } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import type { Observable } from 'rxjs';

import { AntiforgeryService } from '../../../core/api/antiforgery.service';
import { AlgoJudgeAdminApi } from '../../../core/api/admin-generated/algo-judge-admin-api';
import { apiInternalAdminContentBatchesBatchIdGet$Json } from '../../../core/api/admin-generated/fn/content-batches/api-internal-admin-content-batches-batch-id-get-json';
import { apiInternalAdminContentBatchesBatchIdPublishPost$Json } from '../../../core/api/admin-generated/fn/content-batches/api-internal-admin-content-batches-batch-id-publish-post-json';
import { apiInternalAdminContentBatchesBatchIdResumePost$Json } from '../../../core/api/admin-generated/fn/content-batches/api-internal-admin-content-batches-batch-id-resume-post-json';
import { apiInternalAdminContentBatchesBatchIdRetryPost$Json } from '../../../core/api/admin-generated/fn/content-batches/api-internal-admin-content-batches-batch-id-retry-post-json';
import { apiInternalAdminContentBatchesBatchIdStartPost$Json } from '../../../core/api/admin-generated/fn/content-batches/api-internal-admin-content-batches-batch-id-start-post-json';
import { apiInternalAdminContentBatchesGet$Json } from '../../../core/api/admin-generated/fn/content-batches/api-internal-admin-content-batches-get-json';
import type { ContentBatchResponse } from '../../../core/api/admin-generated/models/content-batch-response';
import type { PagedResponseOfContentBatchListItemResponse } from '../../../core/api/admin-generated/models/paged-response-of-content-batch-list-item-response';
import { mapProblemDetails } from '../../../core/api/problem-details.mapper';

@Injectable({ providedIn: 'root' })
export class ContentBatchGateway {
  private readonly api = inject(AlgoJudgeAdminApi);
  private readonly antiforgery = inject(AntiforgeryService);

  list(): Observable<PagedResponseOfContentBatchListItemResponse> {
    return this.wrap(
      this.api.invoke(apiInternalAdminContentBatchesGet$Json, {
        PageNumber: 1,
        PageSize: 100,
      }),
    );
  }

  get(batchId: string): Observable<ContentBatchResponse> {
    return this.wrap(this.api.invoke(apiInternalAdminContentBatchesBatchIdGet$Json, { batchId }));
  }

  start(batchId: string): Observable<ContentBatchResponse> {
    return this.unsafe(
      this.api.invoke(apiInternalAdminContentBatchesBatchIdStartPost$Json, { batchId }),
    );
  }

  resume(batchId: string): Observable<ContentBatchResponse> {
    return this.unsafe(
      this.api.invoke(apiInternalAdminContentBatchesBatchIdResumePost$Json, { batchId }),
    );
  }

  retry(batchId: string, itemIds: readonly string[]): Observable<ContentBatchResponse> {
    return this.unsafe(
      this.api.invoke(apiInternalAdminContentBatchesBatchIdRetryPost$Json, {
        batchId,
        body: { itemIds: [...itemIds] },
      }),
    );
  }

  publish(batchId: string, revisionIds: readonly string[]): Observable<ContentBatchResponse> {
    return this.unsafe(
      this.api.invoke(apiInternalAdminContentBatchesBatchIdPublishPost$Json, {
        batchId,
        body: { revisionIds: [...revisionIds] },
      }),
    );
  }

  private unsafe<T>(source: Observable<T>): Observable<T> {
    return this.antiforgery.ensureToken().pipe(
      switchMap(() => source),
      catchError((error: unknown) => this.rethrow(error, true)),
    );
  }

  private wrap<T>(source: Observable<T>): Observable<T> {
    return source.pipe(catchError((error: unknown) => this.rethrow(error)));
  }

  private rethrow(error: unknown, unsafe = false): Observable<never> {
    const problem = mapProblemDetails(error);
    if (unsafe && problem.code === 'csrf') this.antiforgery.invalidate();
    return throwError(() => problem);
  }
}
