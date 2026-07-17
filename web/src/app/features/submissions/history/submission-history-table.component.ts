import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import type { Submission } from '../data-access/submission.models';
import { SubmissionVerdictComponent } from '../ui/submission-verdict.component';

@Component({
  selector: 'aj-submission-history-table',
  imports: [DatePipe, RouterLink, SubmissionVerdictComponent],
  templateUrl: './submission-history-table.component.html',
  styleUrl: './submission-history-table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubmissionHistoryTableComponent {
  readonly submissions = input.required<readonly Submission[]>();
}
