import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';

@Component({
  selector: 'aj-pagination',
  templateUrl: './pagination.component.html',
  styleUrl: './pagination.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaginationComponent {
  readonly page = input.required<number>();
  readonly totalPages = input.required<number>();
  readonly totalCount = input.required<number>();
  readonly disabled = input(false);
  readonly pageChange = output<number>();

  protected readonly summary = computed(() => {
    if (this.totalCount() === 0) return 'No results';
    return `Page ${this.page()} of ${this.totalPages()} · ${this.totalCount()} results`;
  });

  protected previous(): void {
    if (!this.disabled() && this.page() > 1) this.pageChange.emit(this.page() - 1);
  }

  protected next(): void {
    if (!this.disabled() && this.page() < this.totalPages()) this.pageChange.emit(this.page() + 1);
  }
}
