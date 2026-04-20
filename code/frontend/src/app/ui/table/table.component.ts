import { Component, ChangeDetectionStrategy, input, output } from '@angular/core';
import { SortDirection, type SortState } from './table.types';

@Component({
  selector: 'app-table',
  standalone: true,
  templateUrl: './table.component.html',
  styleUrl: './table.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TableComponent {
  sortKey = input<string | null>(null);
  sortDirection = input<SortDirection>(SortDirection.Desc);
  loading = input(false);
  empty = input(false);

  sortChange = output<SortState>();

  onHeaderClick(key: string | null | undefined): void {
    if (!key) return;
    const currentKey = this.sortKey();
    const currentDir = this.sortDirection();
    if (currentKey !== key) {
      this.sortChange.emit({ sortKey: key, sortDirection: SortDirection.Asc });
      return;
    }
    const next = currentDir === SortDirection.Asc ? SortDirection.Desc : SortDirection.Asc;
    this.sortChange.emit({ sortKey: key, sortDirection: next });
  }
}
