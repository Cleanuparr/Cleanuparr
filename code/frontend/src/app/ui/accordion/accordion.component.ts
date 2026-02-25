import { Component, ChangeDetectionStrategy, input, model } from '@angular/core';
import { NgIcon } from '@ng-icons/core';

@Component({
  selector: 'app-accordion',
  standalone: true,
  imports: [NgIcon],
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

  toggle(): void {
    if (!this.disabled()) {
      this.expanded.update((v) => !v);
    }
  }
}
