import { Component, ChangeDetectionStrategy, input } from '@angular/core';

export type BadgeSeverity = 'default' | 'success' | 'warning' | 'error' | 'info' | 'primary';
export type BadgeSize = 'sm' | 'md';

@Component({
  selector: 'app-badge',
  standalone: true,
  templateUrl: './badge.component.html',
  styleUrl: './badge.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BadgeComponent {
  severity = input<BadgeSeverity>('default');
  size = input<BadgeSize>('md');
  rounded = input(false);
}
