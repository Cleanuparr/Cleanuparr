import { Component, ChangeDetectionStrategy, input, output, computed } from '@angular/core';
import { ButtonComponent } from '../button/button.component';
import { AnimatedCounterComponent } from '../animated-counter/animated-counter.component';
import { SelectComponent, type SelectOption } from '../select/select.component';

@Component({
  selector: 'app-paginator',
  standalone: true,
  imports: [ButtonComponent, AnimatedCounterComponent, SelectComponent],
  templateUrl: './paginator.component.html',
  styleUrl: './paginator.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaginatorComponent {
  totalRecords = input(0);
  pageSize = input(50);
  currentPage = input(1);
  pageSizeOptions = input<number[]>([50, 100, 200, 500]);

  pageChange = output<number>();
  pageSizeChange = output<number>();

  readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.totalRecords() / this.pageSize()))
  );

  readonly canGoPrevious = computed(() => this.currentPage() > 1);
  readonly canGoNext = computed(() => this.currentPage() < this.totalPages());

  readonly displayRange = computed(() => {
    const start = (this.currentPage() - 1) * this.pageSize() + 1;
    const end = Math.min(this.currentPage() * this.pageSize(), this.totalRecords());
    return { start, end };
  });

  readonly pageSizeSelectOptions = computed<SelectOption[]>(() =>
    this.pageSizeOptions().map((n) => ({ label: String(n), value: n }))
  );

  goToPage(page: number): void {
    if (page >= 1 && page <= this.totalPages()) {
      this.pageChange.emit(page);
    }
  }

  previousPage(): void {
    this.goToPage(this.currentPage() - 1);
  }

  nextPage(): void {
    this.goToPage(this.currentPage() + 1);
  }

  onPageSizeChange(value: unknown): void {
    if (typeof value === 'number' && value !== this.pageSize()) {
      this.pageSizeChange.emit(value);
    }
  }
}
