import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

import { submissionStatusLabel, type SubmissionStatus } from '../data-access/submission.models';

@Component({
  selector: 'aj-submission-verdict',
  template: `<span class="verdict" [class]="'verdict verdict--' + status().toLowerCase()">
    @if (status() === 'Accepted') {
      <span aria-hidden="true">✓</span>
    } @else if (status() === 'Pending' || status() === 'Running') {
      <span class="verdict__pulse" aria-hidden="true"></span>
    } @else {
      <span aria-hidden="true">×</span>
    }
    {{ label() }}
  </span>`,
  styleUrl: './submission-verdict.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubmissionVerdictComponent {
  readonly status = input.required<SubmissionStatus>();
  protected readonly label = computed(() => submissionStatusLabel(this.status()));
}
