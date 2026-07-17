import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import type { ProblemDifficulty } from '../data-access/problem.models';

@Component({
  selector: 'aj-problem-difficulty',
  template: `<span
    class="difficulty"
    [class]="'difficulty difficulty--' + difficulty().toLowerCase()"
  >
    {{ difficulty() }}
  </span>`,
  styleUrl: './problem-difficulty.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProblemDifficultyComponent {
  readonly difficulty = input.required<ProblemDifficulty>();
}
