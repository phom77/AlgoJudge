import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'aj-problem-catalogue-page',
  templateUrl: './problem-catalogue.page.html',
  styleUrl: './problem-catalogue.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProblemCataloguePage {}
