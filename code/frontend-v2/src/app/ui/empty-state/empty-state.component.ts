import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { NgIcon } from '@ng-icons/core';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [NgIcon],
  templateUrl: './empty-state.component.html',
  styleUrl: './empty-state.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmptyStateComponent {
  icon = input<string>();
  heading = input<string>();
  description = input<string>();
}
