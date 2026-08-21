import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { BadgeComponent, BadgeSeverity } from '../../ui/badge/badge.component';

@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [BadgeComponent],
  templateUrl: './page-header.component.html',
  styleUrl: './page-header.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PageHeaderComponent {
  title = input.required<string>();
  subtitle = input<string>();
  badge = input<string>();
  badgeSeverity = input<BadgeSeverity>('warning');
}
