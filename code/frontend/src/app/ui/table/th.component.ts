import { Component, ChangeDetectionStrategy, input, inject, computed, HostBinding } from '@angular/core';
import { TableComponent } from './table.component';
import { SortDirection, type ColumnAlign, type ColumnPriority } from './table.types';

@Component({
  selector: 'th[app-th]',
  standalone: true,
  template: `
    @if (sortKey()) {
      <button type="button" class="th-button" (click)="onClick()">
        <span class="th-label"><ng-content /></span>
        <span class="th-indicator" aria-hidden="true">{{ indicator() }}</span>
      </button>
    } @else {
      <span class="th-label"><ng-content /></span>
    }
  `,
  styleUrl: './th.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ThComponent {
  private readonly table = inject(TableComponent, { optional: true });

  sortKey = input<string | null>(null);
  priority = input<ColumnPriority>('primary');
  align = input<ColumnAlign>('left');

  @HostBinding('attr.data-priority') get hostPriority(): ColumnPriority { return this.priority(); }
  @HostBinding('attr.data-align') get hostAlign(): ColumnAlign { return this.align(); }
  @HostBinding('attr.data-sortable') get hostSortable(): string | null { return this.sortKey() ? 'true' : null; }
  @HostBinding('attr.data-active') get hostActive(): string | null {
    return this.sortKey() && this.table?.sortKey() === this.sortKey() ? 'true' : null;
  }

  readonly indicator = computed(() => {
    const key = this.sortKey();
    if (!key || !this.table) return '↕';
    if (this.table.sortKey() !== key) return '↕';
    return this.table.sortDirection() === SortDirection.Asc ? '↑' : '↓';
  });

  onClick(): void {
    this.table?.onHeaderClick(this.sortKey());
  }
}
