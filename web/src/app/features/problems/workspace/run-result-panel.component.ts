import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import type { ApiProblem } from '../../../core/error/api-problem';
import { isTerminalRunStatus, type CodeRun } from '../data-access/run.models';

@Component({
  selector: 'aj-run-result-panel',
  templateUrl: './run-result-panel.component.html',
  styleUrl: './run-result-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RunResultPanelComponent {
  readonly run = input<CodeRun | null>(null);
  readonly problem = input<ApiProblem | null>(null);
  readonly dismissed = output<void>();
  protected readonly terminal = computed(
    () => this.run() !== null && isTerminalRunStatus(this.run()!.status),
  );
  protected readonly label = computed(
    () =>
      ({
        Pending: 'Queued',
        Completed: 'Completed',
        Running: 'Running',
        TimeLimitExceeded: 'Time Limit Exceeded',
        MemoryLimitExceeded: 'Memory Limit Exceeded',
        CompileError: 'Compile Error',
        RuntimeError: 'Runtime Error',
      })[this.run()?.status ?? 'Pending'],
  );
}
