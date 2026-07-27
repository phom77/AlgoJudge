import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'aj-forbidden-page',
  imports: [RouterLink],
  templateUrl: './forbidden.page.html',
  styleUrl: './forbidden.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ForbiddenPage {}
