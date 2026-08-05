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
  readonly problemSearchChange = output<string>();
  readonly statusChange = output<SubmissionStatus | null>();
  protected readonly problemSearchControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.maxLength(100)],
  });

  constructor() {
    this.problemSearchControl.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        filter(() => this.problemSearchControl.valid),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((problemSearch) => this.problemSearchChange.emit(problemSearch.trim()));
  }

  ngOnChanges(): void {
    if (this.problemSearchControl.value !== this.query().problemSearch) {
      this.problemSearchControl.setValue(this.query().problemSearch, { emitEvent: false });
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
