import { ChangeDetectionStrategy, Component, input } from '@angular/core';

@Component({
  selector: 'aj-problem-solved-status',
  templateUrl: './problem-solved-status.component.html',
  styleUrl: './problem-solved-status.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProblemSolvedStatusComponent {
  readonly isSolved = input.required<boolean | null>();
  readonly showLabel = input(false);
}
