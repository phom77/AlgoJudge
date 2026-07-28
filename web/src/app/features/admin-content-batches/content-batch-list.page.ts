import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';

import { ContentBatchStore } from './data-access/content-batch.store';
import { batchStatusLabel } from './data-access/content-batch.models';

@Component({
  selector: 'aj-content-batch-list-page',
  imports: [DatePipe, RouterLink],
  providers: [ContentBatchStore],
  templateUrl: './content-batch-list.page.html',
  styleUrl: './content-batch-list.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContentBatchListPage {
  private readonly destroyRef = inject(DestroyRef);
  protected readonly store = inject(ContentBatchStore);
  protected readonly batchStatusLabel = batchStatusLabel;

  constructor() {
    this.store.loadList().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  protected refresh(): void {
    this.store.loadList().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }
}
