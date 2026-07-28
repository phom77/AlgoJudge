import { inject, Injectable } from '@angular/core';
import { switchMap, take, takeWhile, timer } from 'rxjs';
import type { Observable } from 'rxjs';

import type { ContentBatchResponse } from '../../../core/api/admin-generated/models/content-batch-response';
import { ContentBatchGateway } from './content-batch.gateway';
import { isActiveBatchStatus } from './content-batch.models';

const POLL_INTERVAL_MS = 2_500;
const MAXIMUM_POLLS = 240;

@Injectable({ providedIn: 'root' })
export class ContentBatchPollingService {
  private readonly gateway = inject(ContentBatchGateway);

  watch(batchId: string): Observable<ContentBatchResponse> {
    return timer(0, POLL_INTERVAL_MS).pipe(
      take(MAXIMUM_POLLS),
      switchMap(() => this.gateway.get(batchId)),
      takeWhile((batch) => isActiveBatchStatus(batch.status), true),
    );
  }
}
