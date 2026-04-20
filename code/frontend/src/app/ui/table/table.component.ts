import { Component, ChangeDetectionStrategy, input, output } from '@angular/core';
import type { SortDirection, SortState } from './table.types';

@Component({
  selector: 'app-table',
  standalone: true,
  templateUrl: './table.component.html',
  styleUrl: './table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TableComponent {
  sortKey = input<string | null>(null);
  sortDirection = input<SortDirection>('desc');
  loading = input(false);
  empty = input(false);

  sortChange = output<SortState>();

  onHeaderClick(key: string | null | undefined): void {
    if (!key) return;
    const currentKey = this.sortKey();
    const currentDir = this.sortDirection();
    if (currentKey !== key) {
      this.sortChange.emit({ sortKey: key, sortDirection: 'asc' });
      return;
    }
    if (currentDir === 'asc') {
      this.sortChange.emit({ sortKey: key, sortDirection: 'desc' });
      return;
    }
    this.sortChange.emit({ sortKey: null, sortDirection: 'desc' });
  }
}
