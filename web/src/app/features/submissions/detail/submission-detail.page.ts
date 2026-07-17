import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map } from 'rxjs';

import { SubmissionDetailStore } from '../data-access/submission-detail.store';
import { SubmissionResultPanelComponent } from '../ui/submission-result-panel.component';

@Component({
  selector: 'aj-submission-detail-page',
  imports: [DatePipe, RouterLink, SubmissionResultPanelComponent],
  providers: [SubmissionDetailStore],
  templateUrl: './submission-detail.page.html',
  styleUrl: './submission-detail.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubmissionDetailPage {
  private readonly route = inject(ActivatedRoute);
  protected readonly store = inject(SubmissionDetailStore);

  constructor() {
    this.store.connect(this.route.paramMap.pipe(map((params) => params.get('id')?.trim() ?? '')));
  }
}
