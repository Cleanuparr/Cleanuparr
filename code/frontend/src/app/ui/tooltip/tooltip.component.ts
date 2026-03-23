import { Component, ChangeDetectionStrategy, input } from '@angular/core';

@Component({
  selector: 'app-tooltip',
  standalone: true,
  templateUrl: './tooltip.component.html',
  styleUrl: './tooltip.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TooltipComponent {
  text = input.required<string>();
  position = input<'top' | 'bottom'>('top');
}
