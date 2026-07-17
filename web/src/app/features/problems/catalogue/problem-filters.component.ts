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

import type {
  ProblemCatalogueQuery,
  ProblemDifficulty,
  ProblemTag,
} from '../data-access/problem.models';

@Component({
  selector: 'aj-problem-filters',
  imports: [ReactiveFormsModule],
  templateUrl: './problem-filters.component.html',
  styleUrl: './problem-filters.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProblemFiltersComponent implements OnChanges {
  private readonly destroyRef = inject(DestroyRef);
  readonly query = input.required<ProblemCatalogueQuery>();
  readonly authenticated = input.required<boolean>();
  readonly availableTags = input.required<readonly ProblemTag[]>();
  readonly searchChange = output<string>();
  readonly difficultyChange = output<ProblemDifficulty | null>();
  readonly solvedChange = output<boolean | null>();
  readonly tagToggle = output<string>();

  protected readonly searchControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.maxLength(100)],
  });

  constructor() {
    this.searchControl.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        filter(() => this.searchControl.valid),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((search) => this.searchChange.emit(search.trim()));
  }

  ngOnChanges(): void {
    const search = this.query().search;
    if (this.searchControl.value !== search)
      this.searchControl.setValue(search, { emitEvent: false });
  }

  protected changeDifficulty(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.difficultyChange.emit(isDifficulty(value) ? value : null);
  }

  protected changeSolved(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.solvedChange.emit(value === 'true' ? true : value === 'false' ? false : null);
  }

  protected isTagActive(slug: string): boolean {
    return this.query().tags.includes(slug);
  }

  protected isTagAvailable(slug: string): boolean {
    return this.availableTags().some((tag) => tag.slug === slug);
  }
}

function isDifficulty(value: string): value is ProblemDifficulty {
  return value === 'Easy' || value === 'Medium' || value === 'Hard';
}
