import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  output,
} from '@angular/core';
import type { OnChanges } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { debounceTime, distinctUntilChanged, filter } from 'rxjs';

import type { SubmissionHistoryQuery, SubmissionStatus } from '../data-access/submission.models';

@Component({
  selector: 'aj-submission-history-filters',
  imports: [ReactiveFormsModule],
  templateUrl: './submission-history-filters.component.html',
  styleUrl: './submission-history-filters.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SubmissionHistoryFiltersComponent implements OnChanges {
  private readonly destroyRef = inject(DestroyRef);
  readonly query = input.required<SubmissionHistoryQuery>();
  readonly problemIdChange = output<number | null>();
  readonly statusChange = output<SubmissionStatus | null>();
  protected readonly problemIdControl = new FormControl<number | null>(null, {
    validators: [Validators.min(1)],
  });

  constructor() {
    this.problemIdControl.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        filter(() => this.problemIdControl.valid),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((problemId) => this.problemIdChange.emit(problemId));
  }

  ngOnChanges(): void {
    if (this.problemIdControl.value !== this.query().problemId) {
      this.problemIdControl.setValue(this.query().problemId, { emitEvent: false });
    }
  }

  protected changeStatus(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.statusChange.emit(isStatus(value) ? value : null);
  }
}

function isStatus(value: string): value is SubmissionStatus {
  return [
    'Pending',
    'Running',
    'Accepted',
    'WrongAnswer',
    'TimeLimitExceeded',
    'MemoryLimitExceeded',
    'CompileError',
    'RuntimeError',
  ].includes(value);
}
