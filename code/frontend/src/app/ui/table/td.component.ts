import { Component, ChangeDetectionStrategy, input, HostBinding } from '@angular/core';
import type { ColumnAlign, ColumnPriority } from './table.types';

@Component({
  selector: 'td[app-td]',
  standalone: true,
  template: `<ng-content />`,
  styleUrl: './td.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TdComponent {
  priority = input<ColumnPriority>('primary');
  align = input<ColumnAlign>('left');

  @HostBinding('attr.data-priority') get hostPriority(): ColumnPriority { return this.priority(); }
  @HostBinding('attr.data-align') get hostAlign(): ColumnAlign { return this.align(); }
}
