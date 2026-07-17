import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';

import type { ApiProblem } from '../../../core/error/api-problem';
import { isTerminalStatus, type Submission } from '../data-access/submission.models';
import { SubmissionVerdictComponent } from './submission-verdict.component';

@Component({
  selector: 'aj-submission-result-panel',
  imports: [RouterLink, SubmissionVerdictComponent],
  templateUrl: './submission-result-panel.component.html',
  styleUrl: './submission-result-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubmissionResultPanelComponent {
  readonly submission = input<Submission | null>(null);
  readonly problem = input<ApiProblem | null>(null);
  readonly dismissible = input(false);
  readonly showDetailLink = input(true);
  readonly dismissed = output<void>();
  protected readonly message = computed(() => resultMessage(this.submission()));
  protected readonly terminal = computed(() => {
    const submission = this.submission();
    return submission !== null && isTerminalStatus(submission.status);
  });
}

function resultMessage(submission: Submission | null): string {
  switch (submission?.status) {
    case 'Pending':
      return 'Your submission is queued for judging.';
    case 'Running':
      return 'The judge is compiling and running your solution.';
    case 'Accepted':
      return 'Your solution passed every hidden testcase.';
    case 'WrongAnswer':
      return 'The output did not match the expected result.';
    case 'TimeLimitExceeded':
      return 'The solution exceeded the problem time limit.';
    case 'MemoryLimitExceeded':
      return 'The solution exceeded the problem memory limit.';
    case 'CompileError':
      return 'The C++17 compiler could not build this source.';
    case 'RuntimeError':
      return 'The program exited abnormally while being judged.';
    default:
      return '';
  }
}
