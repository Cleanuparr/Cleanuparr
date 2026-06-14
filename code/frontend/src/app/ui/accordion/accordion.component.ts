import { Component, ChangeDetectionStrategy, input, model } from '@angular/core';
import { NgIcon } from '@ng-icons/core';
import { NewBadgeComponent } from '@ui/new-badge/new-badge.component';

@Component({
  selector: 'app-accordion',
  standalone: true,
  imports: [NgIcon, NewBadgeComponent],
  templateUrl: './accordion.component.html',
  styleUrl: './accordion.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccordionComponent {
  header = input.required<string>();
  subtitle = input<string>();
  error = input<string>();
  expanded = model(false);
  disabled = input(false);
  featureId = input<string>();

  toggle(): void {
    if (!this.disabled()) {
      this.expanded.update((v) => !v);
    }
  }
}
