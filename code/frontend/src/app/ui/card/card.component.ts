import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { NewBadgeComponent } from '@ui/new-badge/new-badge.component';

@Component({
  selector: 'app-card',
  standalone: true,
  imports: [NewBadgeComponent],
  templateUrl: './card.component.html',
  styleUrl: './card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CardComponent {
  header = input<string>();
  subtitle = input<string>();
  elevated = input(false);
  interactive = input(false);
  noPadding = input(false);
  featureId = input<string>();
}
