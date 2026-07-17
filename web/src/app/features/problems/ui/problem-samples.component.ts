import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import type { ProblemSample } from '../data-access/problem.models';

@Component({
  selector: 'aj-problem-samples',
  templateUrl: './problem-samples.component.html',
  styleUrl: './problem-samples.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProblemSamplesComponent {
  readonly samples = input.required<readonly ProblemSample[]>();
}
