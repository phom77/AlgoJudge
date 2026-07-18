import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { filter, map } from 'rxjs';

import { AuthStore } from '../../../core/auth/auth.store';
import { SubmissionFlowStore } from '../../submissions/data-access/submission-flow.store';
import { ProblemWorkspaceStore } from '../data-access/problem-workspace.store';
import { ProblemDifficultyComponent } from '../ui/problem-difficulty.component';
import { ProblemSolvedStatusComponent } from '../ui/problem-solved-status.component';
import { ProblemStatementComponent } from '../ui/problem-statement.component';
import { CodeEditorComponent } from './code-editor.component';
import { ProblemExecutionPanelComponent } from './problem-execution-panel.component';
import { RunFlowStore } from '../data-access/run-flow.store';
import type { ProblemDetail } from '../data-access/problem.models';
import type { RunInput } from '../data-access/run.models';

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
    ProblemExecutionPanelComponent,
  ],
  providers: [ProblemWorkspaceStore, SubmissionFlowStore, RunFlowStore],
  templateUrl: './problem-workspace.page.html',
  styleUrl: './problem-workspace.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProblemWorkspacePage {
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly authStore = inject(AuthStore);
  protected readonly store = inject(ProblemWorkspaceStore);
  protected readonly submissionStore = inject(SubmissionFlowStore);
  protected readonly runStore = inject(RunFlowStore);
  protected readonly activeTab = signal<WorkspaceTab>('statement');
  protected readonly sourceCode = signal(CPP17_STARTER);
  protected readonly sourceBytes = computed(
    () => new TextEncoder().encode(this.sourceCode()).byteLength,
  );
  protected readonly sourceValid = computed(
    () => this.sourceCode().trim().length > 0 && this.sourceBytes() <= 65_536,
  );
  protected readonly busy = computed(
    () => this.submissionStore.submitting() || this.runStore.running(),
  );
  protected readonly returnUrl = computed(() => {
    const problem = this.store.detail();
    return problem === null ? '/problems' : `/problems/${problem.slug}`;
  });

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

  protected run(problem: ProblemDetail, input: RunInput): void {
    this.runStore
      .execute(problem.slug, this.sourceCode(), input)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe();
  }

  protected submit(problemId: number): void {
    this.submissionStore
      .submit(problemId, this.sourceCode())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((submission) => {
        if (submission.status === 'Accepted') this.store.markSolved();
      });
  }
}
