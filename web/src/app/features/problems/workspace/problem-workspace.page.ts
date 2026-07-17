import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { filter, map } from 'rxjs';

import { ProblemWorkspaceStore } from '../data-access/problem-workspace.store';
import { ProblemDifficultyComponent } from '../ui/problem-difficulty.component';
import { ProblemSolvedStatusComponent } from '../ui/problem-solved-status.component';
import { ProblemStatementComponent } from '../ui/problem-statement.component';
import { CodeEditorComponent } from './code-editor.component';

type WorkspaceTab = 'statement' | 'code';

const CPP17_STARTER = `#include <bits/stdc++.h>
using namespace std;

int main() {
  ios::sync_with_stdio(false);
  cin.tie(nullptr);

  return 0;
}
`;

@Component({
  selector: 'aj-problem-workspace-page',
  imports: [
    RouterLink,
    CodeEditorComponent,
    ProblemDifficultyComponent,
    ProblemSolvedStatusComponent,
    ProblemStatementComponent,
  ],
  providers: [ProblemWorkspaceStore],
  templateUrl: './problem-workspace.page.html',
  styleUrl: './problem-workspace.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProblemWorkspacePage {
  private readonly route = inject(ActivatedRoute);
  protected readonly store = inject(ProblemWorkspaceStore);
  protected readonly activeTab = signal<WorkspaceTab>('statement');
  protected readonly sourceCode = signal(CPP17_STARTER);

  constructor() {
    const slug$ = this.route.paramMap.pipe(
      map((params) => params.get('slug')?.trim() ?? ''),
      filter((slug) => slug.length > 0),
    );
    this.store.connect(slug$);
  }

  protected selectTab(tab: WorkspaceTab): void {
    this.activeTab.set(tab);
  }
}
