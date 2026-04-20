export type SortDirection = 'asc' | 'desc';

export type ColumnPriority = 'primary' | 'secondary' | 'tertiary';

export type ColumnAlign = 'left' | 'right' | 'center';

export interface SortState {
  sortKey: string | null;
  sortDirection: SortDirection;
}
