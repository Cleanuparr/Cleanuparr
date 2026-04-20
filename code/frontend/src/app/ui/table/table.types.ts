export enum SortDirection {
  Asc = 'Asc',
  Desc = 'Desc',
}

export type ColumnPriority = 'primary' | 'secondary' | 'tertiary';

export type ColumnAlign = 'left' | 'right' | 'center';

export interface SortState {
  sortKey: string | null;
  sortDirection: SortDirection;
}
