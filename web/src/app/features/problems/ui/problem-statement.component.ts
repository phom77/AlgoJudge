import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { SafeMarkdownComponent } from '../../../shared/ui/markdown/safe-markdown.component';
import type { ProblemDetail } from '../data-access/problem.models';
import { ProblemSamplesComponent } from './problem-samples.component';

@Component({
  selector: 'aj-problem-statement',
  imports: [SafeMarkdownComponent, ProblemSamplesComponent],
  templateUrl: './problem-statement.component.html',
  styleUrl: './problem-statement.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProblemStatementComponent {
  readonly problem = input.required<ProblemDetail>();
}
