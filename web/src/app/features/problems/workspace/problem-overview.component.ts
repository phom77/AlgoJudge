import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import type { ProblemDetail } from '../data-access/problem.models';
import { ProblemDifficultyComponent } from '../ui/problem-difficulty.component';
import { cpp17Type } from './cpp17-starter';

@Component({
  selector: 'aj-problem-overview',
  imports: [ProblemDifficultyComponent],
  templateUrl: './problem-overview.component.html',
  styleUrl: './problem-overview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProblemOverviewComponent {
  readonly problem = input.required<ProblemDetail>();
  protected readonly cppType = cpp17Type;
}
