import { ChangeDetectionStrategy, Component } from '@angular/core';

import { AppShell } from './core/layout/app-shell/app-shell';

@Component({
  selector: 'aj-root',
  imports: [AppShell],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {}
