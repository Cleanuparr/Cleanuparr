import { Component, ChangeDetectionStrategy, input } from '@angular/core';

@Component({
  selector: 'app-card',
  standalone: true,
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
}
