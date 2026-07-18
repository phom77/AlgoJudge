import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import type { ApiProblem } from '../../../core/error/api-problem';
import type { Submission } from '../../submissions/data-access/submission.models';
import { SubmissionResultPanelComponent } from '../../submissions/ui/submission-result-panel.component';
import type { ProblemDetail } from '../data-access/problem.models';
import type { CodeRun, RunInput } from '../data-access/run.models';
import { FunctionArgumentsEditorComponent } from './function-arguments-editor.component';
import { RunResultPanelComponent } from './run-result-panel.component';

type ExecutionAction = 'run' | 'submit';

@Component({
  selector: 'aj-problem-execution-panel',
  imports: [
    RouterLink,
    FunctionArgumentsEditorComponent,
    RunResultPanelComponent,
    SubmissionResultPanelComponent,
  ],
  templateUrl: './problem-execution-panel.component.html',
  styleUrl: './problem-execution-panel.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProblemExecutionPanelComponent {
  readonly problem = input.required<ProblemDetail>();
  readonly authenticated = input(false);
  readonly returnUrl = input('/problems');
  readonly sourceValid = input(false);
  readonly sourceBytes = input(0);
  readonly busy = input(false);
  readonly runBusy = input(false);
  readonly submissionBusy = input(false);
  readonly run = input<CodeRun | null>(null);
  readonly runProblem = input<ApiProblem | null>(null);
  readonly submission = input<Submission | null>(null);
  readonly submissionProblem = input<ApiProblem | null>(null);
  readonly runRequested = output<RunInput>();
  readonly submitRequested = output<void>();
  readonly runDismissed = output<void>();
  readonly submissionDismissed = output<void>();

  protected readonly action = signal<ExecutionAction>('run');
  protected readonly customInput = signal('');
  protected readonly functionArguments = signal<Readonly<Record<string, unknown>> | null>(null);
  protected readonly inputBytes = computed(
    () => new TextEncoder().encode(this.customInput()).byteLength,
  );
  protected readonly canRun = computed(
    () =>
      this.sourceValid() &&
      this.inputBytes() <= 65_536 &&
      (this.problem().executionMode !== 'Function' || this.functionArguments() !== null),
  );

  protected select(action: ExecutionAction): void {
    this.action.set(action);
  }
  protected requestRun(): void {
    this.runRequested.emit(
      this.problem().executionMode === 'Function'
        ? { arguments: this.functionArguments() ?? undefined }
        : { input: this.customInput() },
    );
  }
}
