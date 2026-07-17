import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import type { ProblemListItem } from '../data-access/problem.models';
import { ProblemDifficultyComponent } from '../ui/problem-difficulty.component';
import { ProblemSolvedStatusComponent } from '../ui/problem-solved-status.component';

@Component({
  selector: 'aj-problem-table',
  imports: [RouterLink, ProblemDifficultyComponent, ProblemSolvedStatusComponent],
  templateUrl: './problem-table.component.html',
  styleUrl: './problem-table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProblemTableComponent {
  readonly problems = input.required<readonly ProblemListItem[]>();
}
